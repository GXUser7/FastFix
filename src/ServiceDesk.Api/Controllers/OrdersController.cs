using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Controllers;

/// <summary>Заявки: список, карточка, оформление, жизненный цикл, работы, запчасти, согласование, оплата, выдача</summary>
[ApiController, Authorize, Route("api/orders")]
public class OrdersController : ApiController
{
    private readonly OrderService _orders;
    private readonly DocumentService _documents;

    public OrdersController(OrderService orders, DocumentService documents)
    {
        _orders = orders;
        _documents = documents;
    }

    /// <summary>Список заявок с учётом роли: клиент — свои, мастер — назначенные, приёмщик — все</summary>
    [HttpGet, Authorize(Roles = Roles.Client + "," + Roles.Receptionist + "," + Roles.Master)]
    public Task<List<OrderListItemDto>> List([FromQuery] OrderQuery q) => _orders.ListAsync(Me, q);

    /// <summary>Карточка заявки</summary>
    [HttpGet("{id:int}")]
    public Task<OrderDetailsDto> Get(int id) => _orders.GetAsync(id, Me);

    /// <summary>Оформление заявки</summary>
    [HttpPost, Authorize(Roles = Roles.Receptionist)]
    public async Task<ActionResult<CreateOrderResponse>> Create(CreateOrderRequest req)
    {
        var id = await _orders.CreateAsync(req, Me);
        return CreatedAtAction(nameof(Get), new { id }, new CreateOrderResponse { Id = id });
    }

    /// <summary>Назначение мастера</summary>
    [HttpPut("{id:int}/master"), Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> AssignMaster(int id, AssignMasterRequest req)
    {
        await _orders.AssignMasterAsync(id, req.MasterId, Me);
        return NoContent();
    }

    /// <summary>Смена статуса (только допустимые переходы для роли)</summary>
    [HttpPost("{id:int}/status"), Authorize(Roles = Roles.Master + "," + Roles.Receptionist)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusRequest req)
    {
        await _orders.ChangeStatusAsync(id, req, Me);
        return NoContent();
    }

    /// <summary>Заключение мастера и гарантийный срок</summary>
    [HttpPut("{id:int}/diagnosis"), Authorize(Roles = Roles.Master)]
    public async Task<IActionResult> Diagnosis(int id, DiagnosisRequest req)
    {
        await _orders.SaveDiagnosisAsync(id, req, Me);
        return NoContent();
    }

    /// <summary>Добавление работы в журнал</summary>
    [HttpPost("{id:int}/works"), Authorize(Roles = Roles.Master)]
    public async Task<IActionResult> AddWork(int id, AddWorkRequest req)
    {
        var workId = await _orders.AddWorkAsync(id, req, Me);
        return StatusCode(StatusCodes.Status201Created, new { id = workId });
    }

    /// <summary>Удаление работы из журнала</summary>
    [HttpDelete("{id:int}/works/{workId:int}"), Authorize(Roles = Roles.Master)]
    public async Task<IActionResult> DeleteWork(int id, int workId)
    {
        await _orders.DeleteWorkAsync(id, workId, Me);
        return NoContent();
    }

    /// <summary>Запрос и резервирование запчасти</summary>
    [HttpPost("{id:int}/parts"), Authorize(Roles = Roles.Master)]
    public async Task<ActionResult<ReservationDto>> AddPart(int id, AddPartRequest req) =>
        StatusCode(StatusCodes.Status201Created, await _orders.AddPartAsync(id, req, Me));

    /// <summary>Отмена резерва запчасти</summary>
    [HttpDelete("{id:int}/parts/{reservationId:int}"), Authorize(Roles = Roles.Master)]
    public async Task<IActionResult> DeletePart(int id, int reservationId)
    {
        await _orders.DeletePartAsync(id, reservationId, Me);
        return NoContent();
    }

    /// <summary>Согласование или отказ клиента</summary>
    [HttpPost("{id:int}/decision"), Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Decision(int id, DecisionRequest req)
    {
        await _orders.DecideAsync(id, req, Me);
        return NoContent();
    }

    /// <summary>Приём оплаты (в том числе частичной)</summary>
    [HttpPost("{id:int}/payments"), Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> Pay(int id, PaymentRequest req)
    {
        await _orders.PayAsync(id, req, Me);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Выдача устройства клиенту</summary>
    [HttpPost("{id:int}/issue"), Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> Issue(int id)
    {
        await _orders.IssueAsync(id, Me);
        return NoContent();
    }

    /// <summary>PDF-документ: receipt, estimate, act, issue</summary>
    [HttpGet("{id:int}/documents/{type}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Document(int id, string type)
    {
        var (pdf, name) = await _documents.BuildAsync(id, type, Me);
        return File(pdf, "application/pdf", name);
    }
}
