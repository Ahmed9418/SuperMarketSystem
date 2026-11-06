using Supermarket.Admin.Services;

namespace Supermarket.Admin.Tests;

public sealed class MockDialogService : IDialogService
{
    public List<string> Informations { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();

    public bool ConfirmResult { get; set; } = true;
    public string? PromptResult { get; set; } = "test_prompt_result";

    public void ShowInformation(string message, string? title = null) =>
        Informations.Add($"{title ?? "معلومة"}: {message}");

    public void ShowWarning(string message, string? title = null) =>
        Warnings.Add($"{title ?? "تنبيه"}: {message}");

    public void ShowError(string message, string? title = null) =>
        Errors.Add($"{title ?? "خطأ"}: {message}");

    public bool Confirm(string message, string? title = null) => ConfirmResult;

    public string? Prompt(string message, string? title = null, string? defaultValue = null) =>
        PromptResult ?? defaultValue;
}
