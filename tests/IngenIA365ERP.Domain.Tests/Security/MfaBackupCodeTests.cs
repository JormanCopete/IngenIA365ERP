using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Security;

namespace IngenIA365ERP.Domain.Tests.Security;

/// <summary>
/// T040 — Invariantes del backup code MFA (FR-013):
///   1. <c>MarkUsed</c> fija <c>UsedAt</c> una sola vez (idempotencia).
///   2. Al regenerar el set, los códigos previos quedan invalidados como bloque
///      (mismo <c>BatchId</c> antes / nuevo <c>BatchId</c> ahora).
/// </summary>
public class MfaBackupCodeTests
{
    [Fact]
    public void MarkUsed_sets_UsedAt_first_time()
    {
        var code = new MfaBackupCode { UserId = 1, CodeHash = "h", BatchId = Guid.NewGuid() };
        var t = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);

        code.MarkUsed(t);

        code.UsedAt.Should().Be(t);
    }

    [Fact]
    public void MarkUsed_is_idempotent_after_first_call()
    {
        var first = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        var second = first.AddMinutes(10);
        var code = new MfaBackupCode { UserId = 1, CodeHash = "h", BatchId = Guid.NewGuid() };

        code.MarkUsed(first);
        code.MarkUsed(second); // no-op

        code.UsedAt.Should().Be(first);
    }

    [Fact]
    public void Regenerating_assigns_new_batch_distinct_from_old()
    {
        var oldBatch = Guid.NewGuid();
        var oldCodes = Enumerable.Range(0, 10)
            .Select(_ => new MfaBackupCode { UserId = 1, CodeHash = "h", BatchId = oldBatch })
            .ToList();

        var newBatch = Guid.NewGuid();
        var newCodes = Enumerable.Range(0, 10)
            .Select(_ => new MfaBackupCode { UserId = 1, CodeHash = "h", BatchId = newBatch })
            .ToList();

        oldBatch.Should().NotBe(newBatch);
        oldCodes.Select(c => c.BatchId).Should().AllBeEquivalentTo(oldBatch);
        newCodes.Select(c => c.BatchId).Should().AllBeEquivalentTo(newBatch);
    }
}
