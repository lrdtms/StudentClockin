using Microsoft.Maui.Controls;
using StudentLog.Application.Interfaces;

namespace StudentLog.UI.Services;

public class MauiDialogService : IDialogService
{
    public Task<string?> PromptAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel",
        string? placeholder = null)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Microsoft.Maui.Controls.Application.Current?.Windows[0].Page;
            if (page is null)
                return null;

            return await page.DisplayPromptAsync(title, message, accept, cancel, placeholder);
        });
    }

    public Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Microsoft.Maui.Controls.Application.Current?.Windows[0].Page;
            if (page is null)
                return;

            await page.DisplayAlertAsync(title, message, cancel);
        });
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Microsoft.Maui.Controls.Application.Current?.Windows[0].Page;
            if (page is null)
                return false;

            return await page.DisplayAlertAsync(title, message, accept, cancel);
        });
    }
}
