// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using CleanArchitecture.Blazor.Application.Common.Interfaces.Serialization;
using CleanArchitecture.Blazor.Application.Features.Products.DTOs;
using CleanArchitecture.Blazor.Application.Features.Products.Queries.Specification;
using CleanArchitecture.Blazor.Domain.Enums;

namespace CleanArchitecture.Blazor.Application.Features.Products.Queries.Export;

public class ExportProductsQuery :  IRequest<Result<byte[]>>
{
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Unit { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinPrice { get; set; }
    public string? Keyword { get; set; }
    
    public string? OrderBy { get; set; }
    public string? SortDirection { get; set; }
    public ProductListView ListView { get; set; } = ProductListView.All; //<-- When the user selects a different ListView,
    public UserProfile? CurrentUser { get; set; } // <-- This CurrentUser property gets its value from the information of

    public ExportType ExportType { get; set; }
    public ExportProductSpecification Specification => new ExportProductSpecification(this);
}

public class ExportProductsQueryHandler :
    IRequestHandler<ExportProductsQuery, Result<byte[]>>
{
    private readonly IApplicationDbContext _context;
    private readonly IExcelService _excelService;
    private readonly IStringLocalizer<ExportProductsQueryHandler> _localizer;
    private readonly IMapper _mapper;
    private readonly IPDFService _pdfService;
    private readonly ISerializer _serializer;

    public ExportProductsQueryHandler(
        IApplicationDbContext context,
        IMapper mapper,
        ISerializer serializer,
        IExcelService excelService,
        IPDFService pdfService,
        IStringLocalizer<ExportProductsQueryHandler> localizer
    )
    {
        _context = context;
        _mapper = mapper;
        _serializer = serializer;
        _excelService = excelService;
        _pdfService = pdfService;
        _localizer = localizer;
    }

    public async Task<Result<byte[]>> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
    {
        var data = await _context.Products.ApplySpecification(request.Specification)
            .AsNoTracking()
            .ProjectTo<ProductDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);


        byte[] result;
        Dictionary<string, Func<ProductDto, object?>> mappers;
        switch (request.ExportType)
        {
            case ExportType.PDF:
                mappers = new Dictionary<string, Func<ProductDto, object?>>
                {
                    { _localizer["Brand Name"], item => item.Brand },
                    { _localizer["Product Name"], item => item.Name },
                    { _localizer["Description"], item => item.Description },
                    { _localizer["Price of unit"], item => item.Price },
                    { _localizer["Unit"], item => item.Unit }
                    //{ _localizer["Pictures"], item => string.Join(",",item.Pictures??new string[]{ }) },
                };
                result = await _pdfService.ExportAsync(data, mappers, _localizer["Products"], true);
                break;
            default:
                mappers = new Dictionary<string, Func<ProductDto, object?>>
                {
                    { _localizer["Brand Name"], item => item.Brand },
                    { _localizer["Product Name"], item => item.Name },
                    { _localizer["Description"], item => item.Description },
                    { _localizer["Price of unit"], item => item.Price },
                    { _localizer["Unit"], item => item.Unit },
                    { _localizer["Pictures"], item => _serializer.Serialize(item.Pictures) }
                };
                result = await _excelService.ExportAsync(data, mappers, _localizer["Products"]);
                break;
        }

        return await Result<byte[]>.SuccessAsync(result);
    }
}