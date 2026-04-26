using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Xunit;

namespace AyvalikBankLA.Tests;

public class PasswordValidationServiceTests
{
    private readonly PasswordValidationService _service = new();

    [Fact]
    public void AcceptsValidPassword()
    {
        var act = () => _service.Validate("Valid@123");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("ThisIsWayTooLong1!")]
    public void RejectsOutOfRangeLength(string password)
    {
        var act = () => _service.Validate(password);
        act.Should().Throw<InvalidPasswordException>().WithMessage("*8 and 16*");
    }

    [Fact]
    public void RejectsMissingUppercase()
    {
        var act = () => _service.Validate("nouppercase1!");
        act.Should().Throw<InvalidPasswordException>().WithMessage("*uppercase*");
    }

    [Fact]
    public void RejectsMissingDigit()
    {
        var act = () => _service.Validate("NoDigitHere!");
        act.Should().Throw<InvalidPasswordException>().WithMessage("*digit*");
    }

    [Fact]
    public void RejectsMissingSpecialCharacter()
    {
        var act = () => _service.Validate("NoSpecial123");
        act.Should().Throw<InvalidPasswordException>().WithMessage("*special*");
    }
}
