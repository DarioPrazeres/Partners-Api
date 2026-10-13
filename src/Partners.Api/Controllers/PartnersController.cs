using Microsoft.AspNetCore.Mvc;
using Partners.Core.Contracts;
using Partners.Core.Services;

namespace Partners.Api.Controllers;

[ApiController]
[Route("api/partners")]
public sealed class PartnersController(PartnerService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PartnerResponse>> Create([FromBody] CreatePartnerRequest? request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PartnerResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResponse<PartnerResponse>>> List([FromQuery] PartnerListRequest query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpPut("{id}")]
    public async Task<ActionResult<PartnerResponse>> Update(Guid id, [FromBody] UpdatePartnerRequest? request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));
}
