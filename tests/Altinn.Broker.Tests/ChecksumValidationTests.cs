using System.ComponentModel.DataAnnotations;

using Altinn.Broker.Enums;
using Altinn.Broker.Models;
using Altinn.Broker.Tests.Factories;

using Xunit;

namespace Altinn.Broker.Tests;

public class ChecksumValidationTests
{
    [Theory]
    [InlineData(null, ChecksumAlgorithmExt.MD5, true)]
    [InlineData("", ChecksumAlgorithmExt.MD5, true)]
    [InlineData("d41d8cd98f00b204e9800998ecf8427e", ChecksumAlgorithmExt.MD5, true)]
    [InlineData("D41D8CD98F00B204E9800998ECF8427E", ChecksumAlgorithmExt.MD5, false)]
    [InlineData("abc", ChecksumAlgorithmExt.MD5, false)]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ChecksumAlgorithmExt.SHA256, true)]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", ChecksumAlgorithmExt.SHA256, false)]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ChecksumAlgorithmExt.MD5, false)]
    [InlineData("d41d8cd98f00b204e9800998ecf8427e", ChecksumAlgorithmExt.SHA256, false)]
    public void Checksum_is_validated_against_the_selected_algorithm(string? checksum, ChecksumAlgorithmExt algorithm, bool expectedValid)
    {
        var model = FileTransferInitializeExtTestFactory.BasicFileTransfer();
        model.Checksum = checksum;
        model.ChecksumAlgorithm = algorithm;

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void ChecksumAlgorithm_defaults_to_MD5()
    {
        var model = new FileTransferInitalizeExt();

        Assert.Equal(ChecksumAlgorithmExt.MD5, model.ChecksumAlgorithm);
    }
}
