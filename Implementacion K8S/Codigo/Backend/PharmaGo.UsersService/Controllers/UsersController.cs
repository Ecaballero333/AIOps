using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.UsersService.Models.In;
using PharmaGo.UsersService.Models.Out;

namespace PharmaGo.UsersService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUsersManager _userManager;
        private readonly IStructuredLogger _structuredLogger;

        public UsersController(IUsersManager manager, IStructuredLogger structuredLogger)
        {
            _userManager = manager;
            _structuredLogger = structuredLogger;
        }

        [HttpPost]
        public IActionResult CreateUser([FromBody] UserModelRequest userModel)
        {
            try
            {
                var user = _userManager.CreateUser(userModel.UserName, userModel.UserCode,
                                                   userModel.Email, userModel.Password,
                                                   userModel.Address, userModel.RegistrationDate);
                var userModelResponse = new UserModelResponse(user);
                _structuredLogger.LogInformation(
                    "User created",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "user_create",
                        ["component"] = "UsersController",
                        ["operation"] = "create_user",
                        ["db_operation"] = "insert_user_and_update_invitation",
                        ["outcome"] = "success",
                        ["user_name"] = user.UserName,
                        ["user_email"] = user.Email,
                        ["user_code"] = userModel.UserCode
                    });
                return Ok(userModelResponse);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "User creation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "user_create_fail",
                        ["component"] = "UsersController",
                        ["operation"] = "create_user",
                        ["db_operation"] = "insert_user_and_update_invitation",
                        ["outcome"] = "failed",
                        ["user_name"] = userModel?.UserName ?? "",
                        ["user_email"] = userModel?.Email ?? "",
                        ["user_code"] = userModel?.UserCode ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }
    }
}

