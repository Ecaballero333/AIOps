using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.Domain.Entities;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.UsersService.Enums;
using PharmaGo.UsersService.Filters;
using PharmaGo.UsersService.Models.In;
using PharmaGo.UsersService.Models.Out;

namespace PharmaGo.UsersService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitationsController : ControllerBase
    {
        private readonly IInvitationManager _invitationManager;
        private readonly IStructuredLogger _structuredLogger;

        public InvitationsController(IInvitationManager manager, IStructuredLogger structuredLogger)
        {
            _invitationManager = manager;
            _structuredLogger = structuredLogger;
        }

        [HttpPost]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator), nameof(RoleType.Owner) })]
        public IActionResult CreateInvitation([FromBody] InvitationModelRequest invitationModel)
        {
            try
            {
                string token = HttpContext.Request.Headers["Authorization"];
                var invitation = _invitationManager.CreateInvitation(token, invitationModel.ToEntity());
                _structuredLogger.LogInformation(
                    "Invitation created",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_create",
                        ["component"] = "InvitationsController",
                        ["operation"] = "create_invitation",
                        ["db_operation"] = "insert_invitation",
                        ["outcome"] = "success",
                        ["invitation_id"] = invitation.Id,
                        ["user_name"] = invitation.UserName,
                        ["user_code"] = invitation.UserCode,
                        ["pharmacy_name"] = invitationModel?.Pharmacy ?? "",
                        ["role_name"] = invitationModel?.Role ?? ""
                    });
                return Ok(new InvitationModelResponse(invitation));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Invitation creation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_create_fail",
                        ["component"] = "InvitationsController",
                        ["operation"] = "create_invitation",
                        ["db_operation"] = "insert_invitation",
                        ["outcome"] = "failed",
                        ["user_name"] = invitationModel?.UserName ?? "",
                        ["user_code"] = invitationModel?.UserCode ?? "",
                        ["pharmacy_name"] = invitationModel?.Pharmacy ?? "",
                        ["role_name"] = invitationModel?.Role ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult GetAll([FromQuery] InvitationSearchCriteriaModelRequest searchCriteria)
        {
            try
            {
                var invitations = _invitationManager.GetAllInvitations(searchCriteria.ToEntity());

                List<InvitationSearchCriteriaModelResponse> result =
                    invitations.Select(invitaion => new InvitationSearchCriteriaModelResponse(invitaion)).ToList();
                _structuredLogger.LogInformation(
                    "Invitations retrieved",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_list",
                        ["component"] = "InvitationsController",
                        ["operation"] = "list_invitations",
                        ["db_operation"] = "read_invitations",
                        ["outcome"] = "success",
                        ["invitations_count"] = result.Count,
                        ["user_name"] = searchCriteria?.UserName ?? "",
                        ["pharmacy_name"] = searchCriteria?.Pharmacy ?? "",
                        ["role_name"] = searchCriteria?.Role ?? ""
                    });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Invitations retrieval failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_list_fail",
                        ["component"] = "InvitationsController",
                        ["operation"] = "list_invitations",
                        ["db_operation"] = "read_invitations",
                        ["outcome"] = "failed",
                        ["user_name"] = searchCriteria?.UserName ?? "",
                        ["pharmacy_name"] = searchCriteria?.Pharmacy ?? "",
                        ["role_name"] = searchCriteria?.Role ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetById([FromRoute] int id)
        {
            try
            {
                Invitation invitation = _invitationManager.GetById(id);
                _structuredLogger.LogInformation(
                    "Invitation retrieved by id",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_get_by_id",
                        ["component"] = "InvitationsController",
                        ["operation"] = "get_invitation_by_id",
                        ["db_operation"] = "read_invitation",
                        ["outcome"] = "success",
                        ["invitation_id"] = invitation.Id,
                        ["user_name"] = invitation.UserName,
                        ["user_code"] = invitation.UserCode
                    });
                return Ok(new InvitationDetailModelResponse(invitation));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Invitation retrieval by id failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_get_by_id_fail",
                        ["component"] = "InvitationsController",
                        ["operation"] = "get_invitation_by_id",
                        ["db_operation"] = "read_invitation",
                        ["outcome"] = "failed",
                        ["invitation_id"] = id,
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpPut("{id}")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult UpdateInvitation([FromRoute] int id, [FromBody] InvitationModelRequest invitationModel)
        {
            try
            {
                var invitation = _invitationManager.UpdateInvitation(id, invitationModel.ToEntity());
                InvitationDetailModelResponse result = new InvitationDetailModelResponse(invitation);
                _structuredLogger.LogInformation(
                    "Invitation updated",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_update",
                        ["component"] = "InvitationsController",
                        ["operation"] = "update_invitation",
                        ["db_operation"] = "update_invitation",
                        ["outcome"] = "success",
                        ["invitation_id"] = invitation.Id,
                        ["user_name"] = invitation.UserName,
                        ["user_code"] = invitation.UserCode
                    });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Invitation update failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_update_fail",
                        ["component"] = "InvitationsController",
                        ["operation"] = "update_invitation",
                        ["db_operation"] = "update_invitation",
                        ["outcome"] = "failed",
                        ["invitation_id"] = id,
                        ["user_name"] = invitationModel?.UserName ?? "",
                        ["user_code"] = invitationModel?.UserCode ?? "",
                        ["pharmacy_name"] = invitationModel?.Pharmacy ?? "",
                        ["role_name"] = invitationModel?.Role ?? "",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

        [HttpGet]
        [Route("[action]")]
        [AuthorizationFilter(new string[] { nameof(RoleType.Administrator) })]
        public IActionResult UserCode()
        {
            try
            {
                var userCode = _invitationManager.CreateUserCode();
                _structuredLogger.LogInformation(
                    "Invitation user code generated",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_user_code_generate",
                        ["component"] = "InvitationsController",
                        ["operation"] = "generate_invitation_user_code",
                        ["outcome"] = "success",
                        ["user_code"] = userCode
                    });
                return Ok(new InvitationUserCodeModelResponse(userCode));
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Invitation user code generation failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "invitation_user_code_generate_fail",
                        ["component"] = "InvitationsController",
                        ["operation"] = "generate_invitation_user_code",
                        ["outcome"] = "failed",
                        ["error_message"] = ex.Message
                    });
                throw;
            }
        }

    }
}

