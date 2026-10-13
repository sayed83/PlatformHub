using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20261013180000_AddBusinessModuleFoundation")]
public sealed class AddBusinessModuleFoundation : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        Create(m, "ProductCategories", "Code nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL");
        Create(m, "ProductBrands", "Code nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL");
        Create(m, "ProductUnits", "Code nvarchar(30) NOT NULL, Name nvarchar(100) NOT NULL, ConversionFactor decimal(18,6) NOT NULL CONSTRAINT DF_ProductUnits_ConversionFactor DEFAULT(1)");
        Create(m, "ProductModels", "Code nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL, BrandId bigint NULL");
        Create(m, "ProductBarcodes", "ProductId bigint NOT NULL, Barcode nvarchar(100) NOT NULL, IsPrimary bit NOT NULL CONSTRAINT DF_ProductBarcodes_IsPrimary DEFAULT(0)");
        Create(m, "Stores", "Code nvarchar(50) NOT NULL, Name nvarchar(150) NOT NULL, Address nvarchar(500) NULL");
        Create(m, "StockMovements", "ProductId bigint NOT NULL, StoreId bigint NOT NULL, MovementType nvarchar(30) NOT NULL, QuantityIn decimal(18,4) NOT NULL, QuantityOut decimal(18,4) NOT NULL, UnitCost decimal(18,4) NOT NULL, ReferenceType nvarchar(40) NOT NULL, ReferenceId bigint NULL, TransactionUtc datetime2 NOT NULL, Notes nvarchar(max) NULL");
        Create(m, "StockSnapshots", "ProductId bigint NOT NULL, StoreId bigint NOT NULL, SnapshotDate date NOT NULL, Quantity decimal(18,4) NOT NULL, AverageUnitCost decimal(18,4) NOT NULL");
        Create(m, "StockTransfers", "TransferNo nvarchar(50) NOT NULL, FromStoreId bigint NOT NULL, ToStoreId bigint NOT NULL, ProductId bigint NOT NULL, Quantity decimal(18,4) NOT NULL, Status nvarchar(20) NOT NULL, TransferDateUtc datetime2 NOT NULL");
        Create(m, "AccountHeads", "Code nvarchar(50) NOT NULL, Name nvarchar(200) NOT NULL, AccountType nvarchar(30) NOT NULL, ParentAccountId bigint NULL, IsControlAccount bit NOT NULL CONSTRAINT DF_AccountHeads_IsControlAccount DEFAULT(0)");
        Create(m, "AccountingVouchers", "VoucherNo nvarchar(50) NOT NULL, VoucherType nvarchar(30) NOT NULL, VoucherDate date NOT NULL, Status nvarchar(20) NOT NULL, Narration nvarchar(max) NULL, TotalDebit decimal(18,4) NOT NULL, TotalCredit decimal(18,4) NOT NULL, PostedUtc datetime2 NULL");
        Create(m, "BusinessDocuments", "DocumentType nvarchar(30) NOT NULL, DocumentNo nvarchar(50) NOT NULL, DocumentDate date NOT NULL, PartyAccountId bigint NULL, PartyName nvarchar(200) NOT NULL, Status nvarchar(20) NOT NULL, Subtotal decimal(18,4) NOT NULL, Discount decimal(18,4) NOT NULL, TaxAmount decimal(18,4) NOT NULL, TotalAmount decimal(18,4) NOT NULL, Notes nvarchar(max) NULL");
        m.Sql("CREATE TABLE dbo.AccountingVoucherLines (Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountingVoucherLines PRIMARY KEY, VoucherId bigint NOT NULL, AccountHeadId bigint NOT NULL, Debit decimal(18,4) NOT NULL, Credit decimal(18,4) NOT NULL, Description nvarchar(300) NULL, CostCenterId bigint NULL, CONSTRAINT FK_AccountingVoucherLines_AccountingVouchers_VoucherId FOREIGN KEY (VoucherId) REFERENCES dbo.AccountingVouchers(Id) ON DELETE CASCADE)");
        m.Sql("CREATE TABLE dbo.BusinessDocumentLines (Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_BusinessDocumentLines PRIMARY KEY, DocumentId bigint NOT NULL, ProductId bigint NOT NULL, StoreId bigint NULL, Quantity decimal(18,4) NOT NULL, UnitPrice decimal(18,4) NOT NULL, Discount decimal(18,4) NOT NULL, TaxAmount decimal(18,4) NOT NULL, LineTotal decimal(18,4) NOT NULL, CONSTRAINT FK_BusinessDocumentLines_BusinessDocuments_DocumentId FOREIGN KEY (DocumentId) REFERENCES dbo.BusinessDocuments(Id) ON DELETE CASCADE)");

        ScopedUnique(m, "ProductCategories", "Code");
        ScopedUnique(m, "ProductBrands", "Code");
        ScopedUnique(m, "ProductUnits", "Code");
        ScopedUnique(m, "ProductModels", "Code");
        ScopedUnique(m, "ProductBarcodes", "Barcode");
        ScopedUnique(m, "Stores", "Code");
        ScopedUnique(m, "StockTransfers", "TransferNo");
        ScopedUnique(m, "AccountHeads", "Code");
        ScopedUnique(m, "AccountingVouchers", "VoucherNo");
        m.Sql("CREATE UNIQUE INDEX IX_StockSnapshots_Workspace_Product_Store_Date ON dbo.StockSnapshots(TenantId, BranchId, ProjectId, ProductId, StoreId, SnapshotDate)");
        m.Sql("CREATE INDEX IX_StockMovements_Workspace_Product_Store_Date ON dbo.StockMovements(TenantId, BranchId, ProjectId, ProductId, StoreId, TransactionUtc)");
        m.Sql("CREATE UNIQUE INDEX IX_BusinessDocuments_Workspace_Type_No ON dbo.BusinessDocuments(TenantId, BranchId, ProjectId, DocumentType, DocumentNo)");
        m.Sql("CREATE INDEX IX_AccountingVoucherLines_VoucherId ON dbo.AccountingVoucherLines(VoucherId)");
        m.Sql("CREATE INDEX IX_BusinessDocumentLines_DocumentId ON dbo.BusinessDocumentLines(DocumentId)");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TABLE dbo.BusinessDocumentLines");
        m.Sql("DROP TABLE dbo.AccountingVoucherLines");
        foreach (var table in new[] { "BusinessDocuments", "AccountingVouchers", "AccountHeads", "StockTransfers", "StockSnapshots", "StockMovements", "Stores", "ProductBarcodes", "ProductModels", "ProductUnits", "ProductBrands", "ProductCategories" })
            m.Sql($"DROP TABLE dbo.[{table}]");
    }

    private static void Create(MigrationBuilder m, string table, string columns) =>
        m.Sql($"CREATE TABLE dbo.[{table}] (Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_{table}] PRIMARY KEY, TenantId bigint NOT NULL, BranchId bigint NULL, ProjectId bigint NULL, IsActive bit NOT NULL CONSTRAINT [DF_{table}_IsActive] DEFAULT(1), CreatedUtc datetime2 NOT NULL, UpdatedUtc datetime2 NOT NULL, {columns})");

    private static void ScopedUnique(MigrationBuilder m, string table, string column) =>
        m.Sql($"CREATE UNIQUE INDEX [IX_{table}_Workspace_{column}] ON dbo.[{table}](TenantId, BranchId, ProjectId, [{column}])");
}
