using System.Windows;
using System.Windows.Controls;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Controls;
using ServiceDesk.Desktop.Services;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Desktop.Views.Pages;

// Карточка заявки: набор действий зависит от роли пользователя и статуса заявки
public partial class OrderCardPage : Page
{
    private readonly int _id;
    private OrderDetailsDto _o;

    public OrderCardPage(int id)
    {
        InitializeComponent();
        _id = id;
        Loaded += async (_, _) =>
        {
            RealtimeClient.OrderUpdated += OnOrderUpdated;
            await LoadAsync();
        };
        Unloaded += (_, _) => RealtimeClient.OrderUpdated -= OnOrderUpdated;
    }

    private async void OnOrderUpdated(OrderUpdatedEvent e)
    {
        if (e.OrderId == _id) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _o = await ApiClient.GetAsync<OrderDetailsDto>($"orders/{_id}");
            Render();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
            MainWindow.GoBack();
        }
    }

    private void Render()
    {
        var o = _o;
        TitleText.Text = $"Заявка № {o.Id} · {o.Device.Title}";
        SubtitleText.Text = $"Принята {Fmt.DateTime(o.CreatedAt)} ({o.ReceptionistName}) · срок {Fmt.Date(o.DueDate)} · мастер: {o.MasterName ?? "не назначен"}"
                            + (o.IsWarranty ? " · гарантийный ремонт" : "");
        StatusBadge.Text = o.StatusName;
        StatusBadge.Color = o.StatusColor;
        OverdueBadge.Visibility = o.IsOverdue ? Visibility.Visible : Visibility.Collapsed;
        Stepper.Show(o.Status, o.StatusColor);

        ClientText.Text = o.Client.FullName;
        ClientContacts.Text = FormatPhone(o.Client.Phone) + (o.Client.Email != null ? " · " + o.Client.Email : "")
                              + (o.Client.IsRegistered ? " · есть личный кабинет" : "");
        DeviceText.Text = o.Device.Title;
        SerialText.Text = "S/N, IMEI: " + (o.Device.SerialNumber ?? "не указан");
        FaultText.Text = o.DeclaredFault;
        var look = o.Completeness.Concat(string.IsNullOrWhiteSpace(o.AppearanceNote) ? Array.Empty<string>() : new[] { o.AppearanceNote }).ToList();
        CompletenessText.Text = look.Count == 0 ? "Только устройство, без видимых повреждений" : string.Join(", ", look);

        // диагностика и ремонт — только назначенный мастер в статусах ремонта
        DiagnosisBox.Text = o.Diagnosis ?? "";
        DiagnosisBox.IsReadOnly = !o.CanEditRepair;
        DiagnosisEdit.Visibility = o.CanEditRepair ? Visibility.Visible : Visibility.Collapsed;
        WarrantyBox.Text = o.WarrantyMonths?.ToString() ?? "";
        WarrantyText.Text = o.CanEditRepair ? "" : o.WarrantyMonths is > 0 ? $"Гарантия на работы: {o.WarrantyMonths} мес." : "";
        WorkButtons.Visibility = PartButtons.Visibility = o.CanEditRepair ? Visibility.Visible : Visibility.Collapsed;
        WorksGrid.ItemsSource = o.Works;
        PartsGrid.ItemsSource = o.Parts;
        HistoryList.ItemsSource = o.History.AsEnumerable().Reverse().ToList();

        WorksTotal.Text = Fmt.Money(o.WorksTotal);
        PartsTotal.Text = Fmt.Money(o.PartsTotal);
        Total.Text = o.IsWarranty ? "0 ₽ (гарантия)" : Fmt.Money(o.TotalCost);
        Paid.Text = Fmt.Money(o.Paid);
        Due.Text = Fmt.Money(o.Due);
        Due.Foreground = o.Due > 0 ? (System.Windows.Media.Brush)FindResource("Overdue") : (System.Windows.Media.Brush)FindResource("Text");
        DecisionText.Text = o.ClientDecision switch
        {
            "approved" => $"Клиент согласовал стоимость {Fmt.DateTime(o.DecisionAt)}",
            "rejected" => $"Клиент отказался от ремонта {Fmt.DateTime(o.DecisionAt)}",
            _ => o.Status == S.Approval ? "Ожидается решение клиента в личном кабинете" : "",
        };
        PaymentsList.ItemsSource = o.Payments;

        InfoText.Text = $"Создана: {Fmt.DateTime(o.CreatedAt)}\nГотова: {Fmt.DateTime(o.ReadyAt)}\nВыдана: {Fmt.DateTime(o.IssuedAt)}";

        RenderActions();
        RenderDocuments();
    }

    private static string FormatPhone(string p) =>
        p is { Length: 12 } ? $"+7 {p.Substring(2, 3)} {p.Substring(5, 3)}-{p.Substring(8, 2)}-{p.Substring(10, 2)}" : p;

    // ------------------------------------------------------------------ действия

    private static string ActionTitle(string status, string current) => status switch
    {
        S.Diagnostics => "Начать диагностику",
        S.Approval => "Отправить смету клиенту",
        S.InWork when current == S.WaitingParts => "Запчасти получены — в работу",
        S.InWork => "Взять в работу (гарантия)",
        S.WaitingParts => "Приостановить: ожидание запчастей",
        S.Ready => "Ремонт завершён — готова к выдаче",
        S.Cancelled => "Отменить заявку",
        _ => status,
    };

    private void RenderActions()
    {
        ActionsPanel.Children.Clear();
        foreach (var st in _o.AllowedStatuses)
        {
            var style = st == S.Cancelled ? "DangerButton" : st == S.Ready ? "SuccessButton" : "PrimaryButton";
            AddAction(ActionTitle(st, _o.Status), style, async () => await ChangeStatusAsync(st));
        }
        if (_o.CanPay) AddAction($"Принять оплату (к оплате {Fmt.Money(_o.Due)})", "PrimaryButton", PayAsync);
        if (_o.CanIssue) AddAction(_o.Status == S.Cancelled ? "Вернуть устройство клиенту" : "Выдать устройство", "SuccessButton", IssueAsync);
        else if (Session.IsReceptionist && _o.Status == S.Ready && _o.Due > 0)
            ActionsPanel.Children.Add(new TextBlock { Text = "Выдача возможна после полной оплаты", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 0, 0, 8) });
        if (_o.CanAssignMaster) AddAction(_o.MasterId == null ? "Назначить мастера" : "Сменить мастера", "SecondaryButton", AssignMasterAsync);
        NoActionsText.Visibility = ActionsPanel.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddAction(string text, string style, Func<Task> action)
    {
        var b = new Button { Content = text, Style = (Style)FindResource(style), Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            try
            {
                await action();
            }
            catch (ApiException ex)
            {
                Dialogs.Error(ex.Message);
            }
            finally
            {
                b.IsEnabled = true;
            }
        };
        ActionsPanel.Children.Add(b);
    }

    private async Task ChangeStatusAsync(string status)
    {
        var name = Session.Status(status)?.Name ?? status;
        var dlg = new FormDialog($"Статус «{name}»", "Подтвердить")
            .Note($"Заявка № {_o.Id} будет переведена в статус «{name}».")
            .Text("comment", status == S.Cancelled ? "Причина отмены *" : "Комментарий (необязательно)", multiline: true);
        if (status == S.Cancelled) dlg.Validate = f => f.GetText("comment") == "" ? "Укажите причину отмены" : null;
        if (dlg.ShowDialog() != true) return;
        await ApiClient.PostAsync($"orders/{_id}/status", new ChangeStatusRequest { Status = status, Comment = dlg.GetText("comment") });
        await LoadAsync();
        if (status == S.Ready && Dialogs.Confirm("Ремонт завершён. Открыть акт выполненных работ для печати?"))
            await DocumentOpener.OpenAsync(_id, DocumentTypes.Act);
    }

    private async Task PayAsync()
    {
        var methods = PaymentMethods.All.Select(m => new IdName { Name = PaymentMethods.Title(m) }).ToList();
        var dlg = new FormDialog("Приём оплаты", "Принять оплату")
            .Note($"К оплате: {Fmt.Money(_o.Due)} из {Fmt.Money(_o.TotalCost)}. Допускается частичная оплата.")
            .Number("amount", "Сумма, ₽", _o.Due)
            .Combo("method", "Способ оплаты", methods, "Name");
        dlg.Validate = f =>
        {
            var a = f.GetDecimal("amount");
            if (a is null or <= 0) return "Введите сумму больше нуля";
            if (a > _o.Due) return $"Сумма превышает остаток к оплате ({Fmt.Money(_o.Due)})";
            return null;
        };
        if (dlg.ShowDialog() != true) return;
        var method = PaymentMethods.All[methods.IndexOf(dlg.GetItem<IdName>("method"))];
        await ApiClient.PostAsync($"orders/{_id}/payments", new PaymentRequest { Amount = dlg.GetDecimal("amount").Value, Method = method });
        await LoadAsync();
    }

    private async Task IssueAsync()
    {
        var text = _o.Status == S.Cancelled
            ? $"Вернуть устройство по отменённой заявке № {_o.Id} клиенту {_o.Client.FullName}?"
            : $"Выдать устройство по заявке № {_o.Id} клиенту {_o.Client.FullName}?";
        if (!Dialogs.Confirm(text)) return;
        await ApiClient.PostAsync($"orders/{_id}/issue");
        await LoadAsync();
        if (Dialogs.Confirm("Устройство выдано. Открыть акт выдачи для печати?"))
            await DocumentOpener.OpenAsync(_id, DocumentTypes.Issue);
    }

    private async Task AssignMasterAsync()
    {
        var masters = Session.Dictionaries.Masters;
        var dlg = new FormDialog("Назначение мастера", "Назначить")
            .Combo("master", "Мастер", masters, "Name", masters.FirstOrDefault(m => m.Id == _o.MasterId));
        if (dlg.ShowDialog() != true) return;
        await ApiClient.PutAsync($"orders/{_id}/master", new AssignMasterRequest { MasterId = dlg.GetItem<IdName>("master").Id });
        await LoadAsync();
    }

    // ------------------------------------------------------------------ ремонт (мастер)

    private async void SaveDiagnosis_Click(object sender, RoutedEventArgs e)
    {
        int? months = null;
        if (WarrantyBox.Text.Trim() != "")
        {
            if (!int.TryParse(WarrantyBox.Text.Trim(), out var m) || m < 0 || m > 36)
            {
                Dialogs.Error("Гарантийный срок — целое число от 0 до 36 месяцев");
                return;
            }
            months = m;
        }
        if (string.IsNullOrWhiteSpace(DiagnosisBox.Text))
        {
            Dialogs.Error("Заполните заключение мастера");
            return;
        }
        try
        {
            await ApiClient.PutAsync($"orders/{_id}/diagnosis", new DiagnosisRequest { Diagnosis = DiagnosisBox.Text.Trim(), WarrantyMonths = months });
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void AddWork_Click(object sender, RoutedEventArgs e)
    {
        var services = new List<ServiceDto> { new() { Id = 0, Name = "Произвольная работа" } };
        services.AddRange(Session.Dictionaries.Services);
        var items = services.Select(s => new IdName { Id = s.Id, Name = s.Id == 0 ? s.Name : $"{s.Name} — {Fmt.Money(s.Price)}" }).ToList();
        var dlg = new FormDialog("Добавить работу", "Добавить", 520)
            .Combo("service", "Услуга из прайс-листа", items, "Name", items.Count > 1 ? items[1] : items[0])
            .Text("description", "Описание (для произвольной работы или уточнение)")
            .Number("price", "Стоимость, ₽ (пусто — по прайс-листу)");
        var combo = dlg.GetCombo("service");
        combo.SelectionChanged += (_, _) =>
        {
            var s = services.First(x => x.Id == ((IdName)combo.SelectedItem).Id);
            dlg.GetTextBox("price").Text = s.Id == 0 ? "" : s.Price.ToString("0.##");
        };
        var first = services.First(x => x.Id == ((IdName)combo.SelectedItem).Id);
        if (first.Id != 0) dlg.GetTextBox("price").Text = first.Price.ToString("0.##");
        dlg.Validate = f =>
        {
            var custom = f.GetItem<IdName>("service").Id == 0;
            if (custom && f.GetText("description") == "") return "Опишите работу";
            if (f.GetText("price") != "" && f.GetDecimal("price") is null or < 0) return "Некорректная стоимость";
            if (custom && f.GetDecimal("price") == null) return "Укажите стоимость работы";
            return null;
        };
        if (dlg.ShowDialog() != true) return;
        var id = dlg.GetItem<IdName>("service").Id;
        try
        {
            await ApiClient.PostAsync($"orders/{_id}/works", new AddWorkRequest
            {
                ServiceId = id == 0 ? null : id,
                Description = dlg.GetText("description"),
                Price = dlg.GetDecimal("price"),
            });
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void DeleteWork_Click(object sender, RoutedEventArgs e)
    {
        if (WorksGrid.SelectedItem is not WorkDto w)
        {
            Dialogs.Info("Выберите работу в таблице");
            return;
        }
        if (!Dialogs.Confirm($"Удалить работу «{w.Description}»?")) return;
        try
        {
            await ApiClient.DeleteAsync($"orders/{_id}/works/{w.Id}");
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void AddPart_Click(object sender, RoutedEventArgs e)
    {
        List<PartDto> parts;
        try
        {
            parts = await ApiClient.GetAsync<List<PartDto>>("parts");
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
            return;
        }
        var items = parts.Select(p => new IdName { Id = p.Id, Name = $"{p.Sku} · {p.Name} — {Fmt.Money(p.Price)} (доступно {p.Available} {p.Unit})" }).ToList();
        var dlg = new FormDialog("Запрос запчасти со склада", "Запросить", 620)
            .Note("Если доступного остатка не хватает, запрос получит статус «Ожидает поступления», а заявка в работе перейдёт в «Ожидание запчастей».")
            .Combo("part", "Запчасть", items, "Name")
            .Number("qty", "Количество", 1);
        dlg.GetCombo("part").IsEditable = true;
        dlg.GetCombo("part").IsTextSearchEnabled = true;
        dlg.Validate = f =>
        {
            if (f.GetItem<IdName>("part") == null) return "Выберите запчасть из списка";
            var q = f.GetInt("qty");
            return q is null or < 1 or > 1000 ? "Количество — от 1 до 1000" : null;
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var res = await ApiClient.PostAsync<ReservationDto>($"orders/{_id}/parts", new AddPartRequest { PartId = dlg.GetItem<IdName>("part").Id, Quantity = dlg.GetInt("qty").Value });
            if (res.Status == ReservationStatuses.Requested)
                Dialogs.Info("Запчасти недостаточно на складе. Запрос ожидает поступления — кладовщик увидит его в списке запросов.");
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void DeletePart_Click(object sender, RoutedEventArgs e)
    {
        if (PartsGrid.SelectedItem is not ReservationDto r)
        {
            Dialogs.Info("Выберите запчасть в таблице");
            return;
        }
        if (!Dialogs.Confirm($"Отменить резерв «{r.PartName}», {r.Quantity} шт.?")) return;
        try
        {
            await ApiClient.DeleteAsync($"orders/{_id}/parts/{r.Id}");
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    // ------------------------------------------------------------------ документы

    private void RenderDocuments()
    {
        DocumentsPanel.Children.Clear();
        foreach (var d in _o.Documents)
        {
            var b = new Button
            {
                Content = d.Available ? $"{d.Name} · {d.Number}" : $"{d.Name} — ещё не сформирован",
                Style = (Style)FindResource("SecondaryButton"),
                IsEnabled = d.Available,
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            b.Click += async (_, _) =>
            {
                try
                {
                    await DocumentOpener.OpenAsync(_id, d.Type);
                }
                catch (Exception ex)
                {
                    Dialogs.Error(ex.Message);
                }
            };
            DocumentsPanel.Children.Add(b);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => MainWindow.GoBack();
}
