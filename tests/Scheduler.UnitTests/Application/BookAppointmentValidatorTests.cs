using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Common;
using Scheduler.Domain.Results;
using static Scheduler.UnitTests.Domain.TestData.TestClock;

namespace Scheduler.UnitTests.Application;

public sealed class BookAppointmentValidatorTests
{
    private static BookAppointmentCommand Command(
        string? startTime = "2026-11-10T09:00:00+07:00",
        string? notes = null,
        string? idempotencyKey = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startTime, notes, idempotencyKey);

    [Fact]
    public void Validate_StartWithOffset_Succeeds()
    {
        var result = BookAppointmentValidator.Validate(Command());

        result.IsSuccess.Should().BeTrue();
        result.Value.Start.Should().Be(Tue(9));
    }

    [Fact]
    public void Validate_StartInUtc_SucceedsWithSameInstant()
    {
        var result = BookAppointmentValidator.Validate(Command("2026-11-10T02:00:00Z"));

        result.Value.Start.Should().Be(Tue(9));
    }

    [Theory]
    [InlineData("2026-11-10T09:10:00+07:00")]
    [InlineData("2026-11-10T09:00:30+07:00")]
    [Trait("AC", "AC-01.15")]
    public void Validate_NotOnQuarterHour_ReturnsInvalidStartTime(string startTime)
    {
        BookAppointmentValidator.Validate(Command(startTime)).Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void Validate_StartWithoutOffset_ReturnsInvalidStartTime()
    {
        BookAppointmentValidator.Validate(Command("2026-11-10T09:00:00")).Error.Should().Be(DomainErrors.InvalidStartTime);
    }

    [Fact]
    [Trait("AC", "AC-01.20")]
    public void Validate_MissingFields_ReturnsValidationFailedWithEveryField()
    {
        var command = new BookAppointmentCommand(null, Guid.Empty, null, null, null, null, null);

        var error = BookAppointmentValidator.Validate(command).Error;

        error.Should().BeOfType<ValidationFailedError>()
            .Which.Errors.Keys.Should().BeEquivalentTo(
                new[] { "customerId", "vehicleId", "dealershipId", "serviceTypeId", "startTime" });
        error!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    [Trait("AC", "AC-01.20")]
    public void Validate_MalformedDate_ReturnsValidationFailed()
    {
        var error = BookAppointmentValidator.Validate(Command("not-a-date")).Error;

        error.Should().BeOfType<ValidationFailedError>().Which.Errors.Should().ContainKey("startTime");
    }

    [Fact]
    [Trait("AC", "AC-01.20")]
    public void Validate_NotesTooLong_ReturnsValidationFailed()
    {
        var error = BookAppointmentValidator.Validate(Command(notes: new string('x', 501))).Error;

        error.Should().BeOfType<ValidationFailedError>().Which.Errors.Should().ContainKey("notes");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk")]
    public void Validate_InvalidIdempotencyKey_ReturnsValidationFailed(string key)
    {
        var error = BookAppointmentValidator.Validate(Command(idempotencyKey: key)).Error;

        error.Should().BeOfType<ValidationFailedError>()
            .Which.Errors.Should().ContainKey(BookAppointmentValidator.IdempotencyKeyField);
    }

    [Fact]
    public void Validate_MalformedFieldAndMisalignedTime_ReportsValidationFirst()
    {
        var command = new BookAppointmentCommand(null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026-11-10T09:10:00+07:00", null, null);

        BookAppointmentValidator.Validate(command).Error!.Code.Should().Be("VALIDATION_FAILED");
    }
}
