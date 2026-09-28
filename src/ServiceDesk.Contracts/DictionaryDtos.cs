namespace ServiceDesk.Contracts;

public class IdName
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class StatusDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsFinal { get; set; }
}

public class CompletenessItemDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
}

public class ServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public class DictionariesDto
{
    public List<StatusDto> Statuses { get; set; } = new();
    public List<IdName> DeviceTypes { get; set; } = new();
    public List<CompletenessItemDto> Completeness { get; set; } = new();
    public List<ServiceDto> Services { get; set; } = new();
    public List<IdName> Masters { get; set; } = new();
}

public class ClientDto
{
    public int Id { get; set; }
    public string LastName { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public bool IsRegistered { get; set; }
    public string FullName { get; set; }
}
