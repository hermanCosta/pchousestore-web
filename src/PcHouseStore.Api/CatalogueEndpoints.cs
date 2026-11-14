using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Api;

public static partial class CatalogueEndpoints
{
    public static IEndpointRouteBuilder MapCatalogueEndpoints(this IEndpointRouteBuilder routes)
    {
        var customers = routes.MapGroup("/api/v1/customers").RequireAuthorization("ShopApi");
        customers.MapPost("/queries", SearchCustomersAsync);
        customers.MapGet("/{id:long}", GetCustomerAsync);
        customers.MapPost("", CreateCustomerAsync);
        customers.MapPut("/{id:long}", UpdateCustomerAsync);
        customers.MapPost("/{id:long}/deactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCustomerStatusAsync(id, false, user, db, context, ct));
        customers.MapPost("/{id:long}/reactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCustomerStatusAsync(id, true, user, db, context, ct));

        var items = routes.MapGroup("/api/v1/catalog-items").RequireAuthorization("ShopApi");
        items.MapPost("/queries", SearchCatalogItemsAsync);
        items.MapGet("/{id:long}", GetCatalogItemAsync);
        items.MapPost("", CreateCatalogItemAsync);
        items.MapPut("/{id:long}", UpdateCatalogItemAsync);
        items.MapPost("/{id:long}/deactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCatalogItemStatusAsync(id, false, user, db, context, ct));
        items.MapPost("/{id:long}/reactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCatalogItemStatusAsync(id, true, user, db, context, ct));

        var categories = routes.MapGroup("/api/v1/product-categories").RequireAuthorization("ShopApi");
        categories.MapPost("/queries", SearchCategoriesAsync);
        categories.MapPost("", CreateCategoryAsync);
        categories.MapPut("/{id:long}", UpdateCategoryAsync);
        categories.MapPost("/{id:long}/deactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCategoryStatusAsync(id, false, user, db, context, ct));
        categories.MapPost("/{id:long}/reactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetCategoryStatusAsync(id, true, user, db, context, ct));

        var faults = routes.MapGroup("/api/v1/faults").RequireAuthorization("ShopApi");
        faults.MapPost("/queries", SearchFaultsAsync);
        faults.MapGet("/{id:long}", GetFaultAsync);
        faults.MapPost("", CreateFaultAsync);
        faults.MapPut("/{id:long}", UpdateFaultAsync);
        faults.MapPost("/{id:long}/deactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetFaultStatusAsync(id, false, user, db, context, ct));
        faults.MapPost("/{id:long}/reactivate", (long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct) => SetFaultStatusAsync(id, true, user, db, context, ct));
        return routes;
    }

    private static async Task<IResult> SearchCustomersAsync(DirectoryQuery request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!ValidQuery(request, context)) return InvalidQuery(context);
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var query = from customer in db.Customers.AsNoTracking()
                    join person in db.People.AsNoTracking() on new { customer.PersonId, customer.CompanyId } equals new { PersonId = person.Id, person.CompanyId }
                    where customer.CompanyId == companyId && (request.IncludeInactive || customer.IsActive)
                    select new { customer, person };
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.person.FirstName.ToLower().Contains(term) || x.person.LastName.ToLower().Contains(term) ||
                x.person.ContactNo.ToLower().Contains(term) || x.customer.PhoneE164.ToLower().Contains(term) ||
                (x.person.Email != null && x.person.Email.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.person.LastName).ThenBy(x => x.person.FirstName).ThenBy(x => x.customer.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<CustomerDto>(rows.Select(x => ToCustomerDto(x.customer, x.person)).ToArray(), total, request.Page, request.PageSize));
    }

    private static async Task<IResult> GetCustomerAsync(long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var row = await (from customer in db.Customers.AsNoTracking()
                         join person in db.People.AsNoTracking() on new { customer.PersonId, customer.CompanyId } equals new { PersonId = person.Id, person.CompanyId }
                         where customer.Id == id && customer.CompanyId == companyId
                         select new { customer, person }).SingleOrDefaultAsync(ct);
        return row is null ? Problem(context, 404, "customer_not_found", "The Customer was not found.") : Results.Ok(ToCustomerDto(row.customer, row.person));
    }

    private static async Task<IResult> CreateCustomerAsync(CustomerWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCustomer(request, out var normalizedPhone, out var displayPhone, out var email);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        if (await db.Customers.AnyAsync(x => x.CompanyId == companyId && x.PhoneE164 == normalizedPhone, ct))
            return Problem(context, 409, "duplicate_customer_phone", "A customer with this phone number already exists in the Company.");
        var now = DateTime.UtcNow;
        var person = new PersonRecord { CompanyId = companyId, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), ContactNo = displayPhone!, Email = email };
        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        var customer = new CustomerRecord { CompanyId = companyId, PersonId = person.Id, PhoneE164 = normalizedPhone!, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_customer_company_phone_e164" })
        { return Problem(context, 409, "duplicate_customer_phone", "A customer with this phone number already exists in the Company."); }
        AddAudit(db, auth.Authorization!, "Customer", customer, "CUSTOMER_CREATED", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/customers/{customer.Id}", ToCustomerDto(customer, person));
    }

    private static async Task<IResult> UpdateCustomerAsync(long id, CustomerWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCustomer(request, out var normalizedPhone, out var displayPhone, out var email);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (customer is null) return Problem(context, 404, "customer_not_found", "The Customer was not found.");
        if (await db.Customers.AnyAsync(x => x.CompanyId == companyId && x.Id != id && x.PhoneE164 == normalizedPhone, ct))
            return Problem(context, 409, "duplicate_customer_phone", "A customer with this phone number already exists in the Company.");
        var person = await db.People.SingleAsync(x => x.Id == customer.PersonId && x.CompanyId == companyId, ct);
        person.FirstName = request.FirstName.Trim();
        person.LastName = request.LastName.Trim();
        person.ContactNo = displayPhone!;
        person.Email = email;
        customer.PhoneE164 = normalizedPhone!;
        customer.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "Customer", customer, "CUSTOMER_UPDATED", customer.UpdatedAt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(ToCustomerDto(customer, person));
    }

    private static async Task<IResult> SetCustomerStatusAsync(long id, bool isActive, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var action = isActive ? "CUSTOMER_REACTIVATE" : "CUSTOMER_DEACTIVATE";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (customer is null) return Problem(context, 404, "customer_not_found", "The Customer was not found.");
        customer.IsActive = isActive;
        customer.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "Customer", customer, action + "D", customer.UpdatedAt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchCatalogItemsAsync(DirectoryQuery request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!ValidQuery(request, context)) return InvalidQuery(context);
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var query = db.CatalogItems.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || (x.Sku != null && x.Sku.ToLower().Contains(term)) ||
                (x.ServiceDescription != null && x.ServiceDescription.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Kind).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<CatalogItemDto>(rows.Select(ToCatalogItemDto).ToArray(), total, request.Page, request.PageSize));
    }

    private static async Task<IResult> GetCatalogItemAsync(long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var item = await db.CatalogItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        return item is null ? Problem(context, 404, "catalog_item_not_found", "The catalogue item was not found.") : Results.Ok(ToCatalogItemDto(item));
    }

    private static async Task<IResult> CreateCatalogItemAsync(CatalogItemWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCatalogItem(request, out var kind, out var sku);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var categoryId = request.ProductCategoryId;
        if (kind == "PRODUCT" && (categoryId is null || !await db.ProductCategories.AnyAsync(x => x.Id == categoryId && x.CompanyId == companyId && x.IsActive, ct)))
            return Problem(context, 400, "invalid_product_category", "Choose an active product category.");
        if (kind == "SERVICE" && (categoryId is not null || string.IsNullOrWhiteSpace(request.Description)))
            return Problem(context, 400, "invalid_service_details", "A Service needs a description and cannot have a product category.");
        if (sku is not null && await db.CatalogItems.AnyAsync(x => x.CompanyId == companyId && x.Sku == sku, ct))
            return Problem(context, 409, "catalog_sku_conflict", "This SKU is already used in the Company.");
        var now = DateTime.UtcNow;
        var item = new CatalogItemRecord
        {
            CompanyId = companyId, Kind = kind!, Name = request.Name.Trim(), Sku = sku,
            ListPrice = request.ListPrice, ProductCategoryId = categoryId,
            InventoryTrackingMode = kind == "PRODUCT" ? NormalizeTrackingMode(request.InventoryTrackingMode) : null,
            ServiceDescription = kind == "SERVICE" ? request.Description!.Trim() : null,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        db.CatalogItems.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_catalog_item_company_sku" })
        { return Problem(context, 409, "catalog_sku_conflict", "This SKU is already used in the Company."); }
        AddAudit(db, auth.Authorization!, "CatalogItem", item, "CATALOG_ITEM_CREATED", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/catalog-items/{item.Id}", ToCatalogItemDto(item));
    }

    private static async Task<IResult> UpdateCatalogItemAsync(long id, CatalogItemWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCatalogItem(request, out var kind, out var sku);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var item = await db.CatalogItems.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (item is null) return Problem(context, 404, "catalog_item_not_found", "The catalogue item was not found.");
        if (item.Kind != kind) return Problem(context, 409, "catalog_item_kind_immutable", "An item cannot change between Product and Service.");
        var categoryId = request.ProductCategoryId;
        if (kind == "PRODUCT" && (categoryId is null || !await db.ProductCategories.AnyAsync(x => x.Id == categoryId && x.CompanyId == companyId && x.IsActive, ct)))
            return Problem(context, 400, "invalid_product_category", "Choose an active product category.");
        if (kind == "SERVICE" && (categoryId is not null || string.IsNullOrWhiteSpace(request.Description)))
            return Problem(context, 400, "invalid_service_details", "A Service needs a description and cannot have a product category.");
        if (sku is not null && await db.CatalogItems.AnyAsync(x => x.CompanyId == companyId && x.Id != id && x.Sku == sku, ct))
            return Problem(context, 409, "catalog_sku_conflict", "This SKU is already used in the Company.");
        item.Name = request.Name.Trim(); item.Sku = sku; item.ListPrice = request.ListPrice; item.ProductCategoryId = categoryId;
        item.InventoryTrackingMode = kind == "PRODUCT" ? NormalizeTrackingMode(request.InventoryTrackingMode) : null;
        item.ServiceDescription = kind == "SERVICE" ? request.Description!.Trim() : null;
        item.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "CatalogItem", item, "CATALOG_ITEM_UPDATED", item.UpdatedAt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(ToCatalogItemDto(item));
    }

    private static async Task<IResult> SetCatalogItemStatusAsync(long id, bool isActive, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var action = isActive ? "CATALOG_ITEM_REACTIVATE" : "CATALOG_ITEM_DEACTIVATE";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var item = await db.CatalogItems.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (item is null) return Problem(context, 404, "catalog_item_not_found", "The catalogue item was not found.");
        item.IsActive = isActive; item.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "CatalogItem", item, action + "D", item.UpdatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchCategoriesAsync(DirectoryQuery request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!ValidQuery(request, context)) return InvalidQuery(context);
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var query = db.ProductCategories.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term) || (x.Description != null && x.Description.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var ids = rows.Where(x => x.ParentCategoryId.HasValue).Select(x => x.ParentCategoryId!.Value).Distinct().ToArray();
        var parents = await db.ProductCategories.AsNoTracking().Where(x => ids.Contains(x.Id) && x.CompanyId == companyId).ToDictionaryAsync(x => x.Id, ct);
        return Results.Ok(new PageResponse<ProductCategoryDto>(rows.Select(x => ToCategoryDto(x, parents.GetValueOrDefault(x.ParentCategoryId ?? 0))).ToArray(), total, request.Page, request.PageSize));
    }

    private static async Task<IResult> CreateCategoryAsync(ProductCategoryWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCategory(request);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.ProductCategories.AnyAsync(x => x.CompanyId == companyId && x.Code == code, ct)) return Problem(context, 409, "category_code_conflict", "This category code is already used in the Company.");
        if (request.ParentCategoryId is long parentId && !await db.ProductCategories.AnyAsync(x => x.Id == parentId && x.CompanyId == companyId && x.IsActive, ct))
            return Problem(context, 400, "invalid_parent_category", "Choose an active parent category in this Company.");
        var now = DateTime.UtcNow;
        var category = new ProductCategoryRecord { CompanyId = companyId, ParentCategoryId = request.ParentCategoryId, Name = request.Name.Trim(), Code = code, Description = CleanOptional(request.Description), IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(ct);
        AddAudit(db, auth.Authorization!, "ProductCategory", category, "PRODUCT_CATEGORY_CREATED", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/product-categories/{category.Id}", ToCategoryDto(category, null));
    }

    private static async Task<IResult> UpdateCategoryAsync(long id, ProductCategoryWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var validation = ValidateCategory(request);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var category = await db.ProductCategories.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (category is null) return Problem(context, 404, "category_not_found", "The Product category was not found.");
        if (request.ParentCategoryId == id) return Problem(context, 400, "invalid_parent_category", "A category cannot be its own parent.");
        if (request.ParentCategoryId is long parentId && !await db.ProductCategories.AnyAsync(x => x.Id == parentId && x.CompanyId == companyId && x.IsActive, ct))
            return Problem(context, 400, "invalid_parent_category", "Choose an active parent category in this Company.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.ProductCategories.AnyAsync(x => x.CompanyId == companyId && x.Id != id && x.Code == code, ct)) return Problem(context, 409, "category_code_conflict", "This category code is already used in the Company.");
        category.ParentCategoryId = request.ParentCategoryId; category.Name = request.Name.Trim(); category.Code = code; category.Description = CleanOptional(request.Description); category.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "ProductCategory", category, "PRODUCT_CATEGORY_UPDATED", category.UpdatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Ok(ToCategoryDto(category, null));
    }

    private static async Task<IResult> SetCategoryStatusAsync(long id, bool isActive, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var action = isActive ? "PRODUCT_CATEGORY_REACTIVATE" : "PRODUCT_CATEGORY_DEACTIVATE";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var category = await db.ProductCategories.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (category is null) return Problem(context, 404, "category_not_found", "The Product category was not found.");
        category.IsActive = isActive; category.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "ProductCategory", category, action + "D", category.UpdatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchFaultsAsync(DirectoryQuery request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!ValidQuery(request, context)) return InvalidQuery(context);
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var query = db.Faults.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Description.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Description).ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<FaultDto>(rows.Select(ToFaultDto).ToArray(), total, request.Page, request.PageSize));
    }

    private static async Task<IResult> GetFaultAsync(long id, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var fault = await db.Faults.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        return fault is null ? Problem(context, 404, "fault_not_found", "The Fault was not found.") : Results.Ok(ToFaultDto(fault));
    }

    private static async Task<IResult> CreateFaultAsync(FaultWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var description = NormalizeFaultDescription(request.Description);
        if (description is null) return Problem(context, 400, "invalid_request", "Enter a Fault description (up to 255 characters).");
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        await db.Companies.FromSqlInterpolated($"SELECT * FROM company WHERE id = {companyId} FOR UPDATE").SingleAsync(ct);
        var sequence = (await db.Faults.Where(x => x.CompanyId == companyId).MaxAsync(x => (long?)x.SequenceNumber, ct) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var fault = new FaultRecord { CompanyId = companyId, SequenceNumber = sequence, Code = $"FLT-{sequence:D6}", Description = description, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Faults.Add(fault);
        await db.SaveChangesAsync(ct);
        AddAudit(db, auth.Authorization!, "Fault", fault, "FAULT_CREATED", now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/faults/{fault.Id}", ToFaultDto(fault));
    }

    private static async Task<IResult> UpdateFaultAsync(long id, FaultWriteRequest request, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        var description = NormalizeFaultDescription(request.Description);
        if (description is null) return Problem(context, 400, "invalid_request", "Enter a Fault description (up to 255 characters).");
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var fault = await db.Faults.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (fault is null) return Problem(context, 404, "fault_not_found", "The Fault was not found.");
        fault.Description = description; fault.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "Fault", fault, "FAULT_UPDATED", fault.UpdatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Ok(ToFaultDto(fault));
    }

    private static async Task<IResult> SetFaultStatusAsync(long id, bool isActive, ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId)) return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");
        var action = isActive ? "FAULT_REACTIVATE" : "FAULT_DEACTIVATE";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var auth = await RequireActiveShopSessionAsync(user, db, context, ct);
        if (auth.Error is not null) return auth.Error;
        var fault = await db.Faults.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);
        if (fault is null) return Problem(context, 404, "fault_not_found", "The Fault was not found.");
        fault.IsActive = isActive; fault.UpdatedAt = DateTime.UtcNow;
        AddAudit(db, auth.Authorization!, "Fault", fault, action + "D", fault.UpdatedAt);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<ShopWriteResult> RequireActiveShopSessionAsync(ClaimsPrincipal user, PcHouseStoreDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryCompany(user, out var companyId) || !TryShop(user, out var shopId) ||
            !long.TryParse(user.FindFirstValue("shop_session_id"), out var sessionId))
            return new(null, Problem(context, 401, "invalid_shop_token", "Shop authentication is required."));

        var session = await db.ShopSessions.FromSqlInterpolated($"SELECT * FROM shop_session WHERE id = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        if (session is null || session.CompanyId != companyId || session.ShopId != shopId || session.RevokedAt is not null ||
            (session.IdleExpiresAt is not null && session.IdleExpiresAt <= now) ||
            (session.AbsoluteExpiresAt is not null && session.AbsoluteExpiresAt <= now) ||
            !await db.Shops.AnyAsync(x => x.Id == shopId && x.CompanyId == companyId && x.IsActive, ct) ||
            !await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, ct))
            return new(null, Problem(context, 401, "shop_session_revoked", "The Shop session is no longer active."));

        return new(new ShopWriteAuditContext(companyId, shopId, sessionId), null);
    }

    private static void AddAudit(PcHouseStoreDbContext db, ShopWriteAuditContext actor, string type, CustomerRecord record, string action, DateTime at) =>
        AddAudit(db, actor, type, record.Id, action, at);
    private static void AddAudit(PcHouseStoreDbContext db, ShopWriteAuditContext actor, string type, CatalogItemRecord record, string action, DateTime at) =>
        AddAudit(db, actor, type, record.Id, action, at);
    private static void AddAudit(PcHouseStoreDbContext db, ShopWriteAuditContext actor, string type, ProductCategoryRecord record, string action, DateTime at) =>
        AddAudit(db, actor, type, record.Id, action, at);
    private static void AddAudit(PcHouseStoreDbContext db, ShopWriteAuditContext actor, string type, FaultRecord record, string action, DateTime at) =>
        AddAudit(db, actor, type, record.Id, action, at);
    private static void AddAudit(PcHouseStoreDbContext db, ShopWriteAuditContext actor, string type, long id, string action, DateTime at) => db.CatalogueAuditEvents.Add(new CatalogueAuditEventRecord
    {
        CompanyId = actor.CompanyId, ShopId = actor.ShopId, ShopSessionId = actor.SessionId, ActorType = "SHOP_SESSION",
        ActorEmployeeId = null, ResourceType = type, ResourceId = id, ActionCode = action, OccurredAt = at
    });

    private sealed record ShopWriteAuditContext(long CompanyId, long ShopId, long SessionId);
    private sealed record ShopWriteResult(ShopWriteAuditContext? Authorization, IResult? Error);

    private static string? ValidateCustomer(CustomerWriteRequest request, out string? phoneE164, out string? displayPhone, out string? email)
    {
        phoneE164 = null; displayPhone = null; email = CleanOptional(request.Email)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 200)
            return "First name and last name are required and must be 200 characters or fewer.";
        if (email is { Length: > 320 }) return "Email must be 320 characters or fewer.";
        if (!TryNormalizePhone(request.Phone, request.IsInternational, out var normalized, out var display))
            return request.IsInternational ? "Enter a valid international phone number including its country code." : "Enter a valid Irish phone number, or select International number.";
        phoneE164 = normalized; displayPhone = display;
        return null;
    }

    private static bool TryNormalizePhone(string? value, bool international, out string e164, out string display)
    {
        e164 = string.Empty; display = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80) return false;
        var input = value.Trim();
        var plus = input.StartsWith('+');
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (international)
        {
            if (input.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
            else if (!plus) return false;
            if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
            e164 = "+" + digits;
        }
        else if (plus && digits.StartsWith("353", StringComparison.Ordinal)) e164 = "+" + digits;
        else if (digits.StartsWith("00353", StringComparison.Ordinal)) e164 = "+" + digits[2..];
        else if (digits.StartsWith("0", StringComparison.Ordinal)) e164 = "+353" + digits[1..];
        else return false;
        if (!E164Regex().IsMatch(e164)) { e164 = string.Empty; return false; }
        display = input;
        return true;
    }

    private static string? ValidateCatalogItem(CatalogItemWriteRequest request, out string? kind, out string? sku)
    {
        kind = request.Kind?.Trim().ToUpperInvariant();
        sku = CleanOptional(request.Sku)?.ToUpperInvariant();
        if (kind is not ("PRODUCT" or "SERVICE")) return "Choose Product or Service.";
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200) return "Name is required and must be 200 characters or fewer.";
        if (sku is { } supplied && !SkuRegex().IsMatch(supplied)) return "SKU must use the format SKU-AB12CD34, or be left blank.";
        if (request.ListPrice < 0 || decimal.Round(request.ListPrice, 2) != request.ListPrice || request.ListPrice >= 100000000000000000m)
            return "Enter a valid price with up to two decimal places.";
        if (kind == "SERVICE" && request.Description?.Trim().Length > 2000) return "Service description must be 2,000 characters or fewer.";
        if (kind == "PRODUCT" && request.InventoryTrackingMode is not null && NormalizeTrackingMode(request.InventoryTrackingMode) is null)
            return "Choose a valid Product stock tracking mode.";
        return null;
    }

    private static string? ValidateCategory(ProductCategoryWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) return "Category name is required and must be 160 characters or fewer.";
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 40) return "Category code is required and must be 40 characters or fewer.";
        if (request.Description?.Length is > 2000) return "Category description must be 2,000 characters or fewer.";
        return null;
    }

    private static string? NormalizeTrackingMode(string? mode) => mode?.Trim().ToUpperInvariant() switch
    {
        null or "" => "QUANTITY",
        "QUANTITY" => "QUANTITY",
        "SERIALIZED_UNIT" => "SERIALIZED_UNIT",
        _ => null
    };

    private static string? NormalizeFaultDescription(string? description)
    {
        var normalized = CleanOptional(description)?.ToUpperInvariant();
        return normalized is { Length: <= 255 } ? normalized : null;
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CustomerDto ToCustomerDto(CustomerRecord customer, PersonRecord person) => new(customer.Id.ToString(CultureInfo.InvariantCulture), person.FirstName, person.LastName, person.ContactNo, customer.PhoneE164, person.Email, customer.IsActive);
    private static CatalogItemDto ToCatalogItemDto(CatalogItemRecord item) => new(item.Id.ToString(CultureInfo.InvariantCulture), item.Kind, item.Name, item.Sku, item.ListPrice, item.ProductCategoryId?.ToString(CultureInfo.InvariantCulture), item.InventoryTrackingMode, item.ServiceDescription, item.IsActive);
    private static ProductCategoryDto ToCategoryDto(ProductCategoryRecord item, ProductCategoryRecord? parent) => new(item.Id.ToString(CultureInfo.InvariantCulture), item.Name, item.Code, item.ParentCategoryId?.ToString(CultureInfo.InvariantCulture), parent?.Name, item.Description, item.IsActive);
    private static FaultDto ToFaultDto(FaultRecord item) => new(item.Id.ToString(CultureInfo.InvariantCulture), item.Code, item.Description, item.IsActive);

    private static bool TryCompany(ClaimsPrincipal user, out long companyId) => long.TryParse(user.FindFirstValue("company_id"), out companyId);
    private static bool TryShop(ClaimsPrincipal user, out long shopId) => long.TryParse(user.FindFirstValue("shop_id"), out shopId);
    private static bool ValidQuery(DirectoryQuery query, HttpContext context) => query.Page >= 1 && query.PageSize is >= 1 and <= 100 && (query.Search?.Length ?? 0) <= 200;
    private static IResult InvalidQuery(HttpContext context) => Problem(context, 400, "invalid_request", "Search text must be 200 characters or fewer, page must be positive, and pageSize must be between 1 and 100.");
    private static IResult Problem(HttpContext context, int status, string code, string detail) => Results.Problem(statusCode: status, title: code, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });
    [GeneratedRegex("^\\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant)] private static partial Regex E164Regex();
    [GeneratedRegex("^SKU-[A-Z0-9]{8}$", RegexOptions.CultureInvariant)] private static partial Regex SkuRegex();
}

public sealed record DirectoryQuery(string? Search = null, bool IncludeInactive = false, int Page = 1, int PageSize = 25);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record CustomerWriteRequest(string FirstName, string LastName, string Phone, bool IsInternational, string? Email);
public sealed record CustomerDto(string Id, string FirstName, string LastName, string Phone, string PhoneE164, string? Email, bool IsActive);
public sealed record CatalogItemWriteRequest(string Kind, string Name, string? Sku, decimal ListPrice, long? ProductCategoryId, string? InventoryTrackingMode, string? Description);
public sealed record CatalogItemDto(string Id, string Kind, string Name, string? Sku, decimal ListPrice, string? ProductCategoryId, string? InventoryTrackingMode, string? Description, bool IsActive);
public sealed record ProductCategoryWriteRequest(string Name, string Code, long? ParentCategoryId, string? Description);
public sealed record ProductCategoryDto(string Id, string Name, string Code, string? ParentCategoryId, string? ParentCategoryName, string? Description, bool IsActive);
public sealed record FaultWriteRequest(string Description);
public sealed record FaultDto(string Id, string Code, string Description, bool IsActive);
