using Microsoft.AspNetCore.SignalR;

public class MqttHub : Hub
{
    // Empty hub – server will push readings using IHubContext<MqttHub>
}
