using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SignalRLearn.Api.Hubs;
using SignalRLearn.Api.Services;
using SignalRLearn.Contracts.Orders;
using SignalRLearn.Contracts.Realtime;

namespace SignalRLearn.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IOrderStore orderStore,
    IHubContext<OrderHub, IOrderClient> hubContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<OrderDto>> GetAll() => Ok(orderStore.GetAll());

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<OrderDto> GetById(Guid id)
    {
        var order = orderStore.GetById(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = orderStore.Create(request);

        // The REST mutation completes first. SignalR then tells connected clients
        // about the new source-of-truth state.
        await hubContext.Clients.All.OrderCreated(order);

        return CreatedAtRoute(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = orderStore.UpdateStatus(id, request.Status);

        if (result.Outcome == UpdateOrderOutcome.NotFound)
        {
            return NotFound();
        }

        if (result.Outcome == UpdateOrderOutcome.InvalidTransition)
        {
            ModelState.AddModelError(nameof(request.Status), result.Error!);
            return ValidationProblem(ModelState);
        }

        if (result.Outcome == UpdateOrderOutcome.Updated)
        {
            await hubContext.Clients.All.OrderStatusChanged(
                new OrderStatusChangedEvent(result.Order!, result.PreviousStatus!.Value));
        }

        return Ok(result.Order);
    }
}
