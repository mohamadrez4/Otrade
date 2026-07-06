namespace Otrade.Application.DTOs.Admin;

public class SystemSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SaveSystemSettingsRequest
{
    public List<SystemSettingDto> Settings { get; set; } = new();
}