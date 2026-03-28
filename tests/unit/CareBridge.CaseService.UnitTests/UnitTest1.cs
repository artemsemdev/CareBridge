using CareBridge.CaseService.Data;
using CareBridge.CaseService.Entities;
using CareBridge.Shared.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CaseService.UnitTests;

public class CaseDbContextTests
{
    private CaseDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CaseDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrieveCase()
    {
        using var context = CreateInMemoryContext();

        var caseEntity = new CaseEntity
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            PatientName = "Jane Smith",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Cases.Add(caseEntity);
        await context.SaveChangesAsync();

        var retrieved = await context.Cases.FindAsync(caseEntity.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Jane Smith", retrieved.PatientName);
        Assert.Equal(CaseStatus.Active, retrieved.Status);
    }

    [Fact]
    public async Task CanListCases()
    {
        using var context = CreateInMemoryContext();

        context.Cases.AddRange(
            new CaseEntity { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), PatientName = "Patient A", DiagnosisCode = "J18.9", DiagnosisDescription = "Pneumonia", DischargeDate = DateTimeOffset.UtcNow, Status = CaseStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new CaseEntity { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), PatientName = "Patient B", DiagnosisCode = "I50.9", DiagnosisDescription = "Heart failure", DischargeDate = DateTimeOffset.UtcNow, Status = CaseStatus.Monitoring, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();

        var cases = await context.Cases.ToListAsync();
        Assert.Equal(2, cases.Count);
    }

    [Fact]
    public async Task CanUpdateCaseStatus()
    {
        using var context = CreateInMemoryContext();

        var caseEntity = new CaseEntity
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            PatientName = "Test Patient",
            DiagnosisCode = "J18.9",
            DiagnosisDescription = "Pneumonia",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Cases.Add(caseEntity);
        await context.SaveChangesAsync();

        caseEntity.Status = CaseStatus.Completed;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        var retrieved = await context.Cases.FindAsync(caseEntity.Id);
        Assert.Equal(CaseStatus.Completed, retrieved!.Status);
    }
}
