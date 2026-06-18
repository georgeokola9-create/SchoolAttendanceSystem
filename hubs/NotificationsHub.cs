using Microsoft.AspNetCore.SignalR;

namespace BiometricAttendanceSystem.Hubs
{
    /// <summary>
    /// Pushes live updates to admin browsers:
    ///  - "PendingCountUpdated" (int count) — badge update when a request is created/approved/rejected
    ///  - "RequestListUpdated"              — triggers page reload on the Device Requests page
    ///  - "AttendanceUpdated"               — fired when a teacher checks in via QR
    /// </summary>
    public class NotificationsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // Only admins join the admins group — but anyone can connect
            if (Context.User?.IsInRole("Administrator") == true)
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.User?.IsInRole("Administrator") == true)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");

            await base.OnDisconnectedAsync(exception);
        }
    }
}
