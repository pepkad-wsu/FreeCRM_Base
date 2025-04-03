using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CICD.Server.Controllers;

public partial class DataController
{
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/Decrypt/")]
    public ActionResult<DataObjects.BooleanResponse> Decrypt(DataObjects.SimplePost post)
    {
        var output = new DataObjects.BooleanResponse();

        string DecryptedText = da.Decrypt(post.SingleItem);
        if (String.IsNullOrWhiteSpace(DecryptedText)) {
            output.Messages.Add("Error Decrypting Text");
        } else {
            output.Messages.Add(DecryptedText);
            output.Result = true;
        }
        return Ok(output);
    }


    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [Route("~/api/Data/Encrypt/")]
    public ActionResult<DataObjects.BooleanResponse> Encrypt(DataObjects.SimplePost post)
    {
        DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();

        string Encrypted = da.Encrypt(post.SingleItem);
        if (String.IsNullOrWhiteSpace(Encrypted)) {
            output.Messages.Add("Error Encrypting Text");
        } else {
            output.Messages.Add(Encrypted);
            output.Result = true;
        }
        return Ok(output);
    }

    [HttpGet]
    [Authorize(Policy = Policies.AppAdmin)]
    [Route("~/api/Data/GetNewEncryptionKey")]
    public ActionResult<DataObjects.BooleanResponse> GetNewEncryptionKey()
    {
        var output = new DataObjects.BooleanResponse {
            Result = true,
            Messages = new List<string> { da.GetNewEncryptionKey() }
        };
        return Ok(output);
    }
}
