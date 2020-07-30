﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TestApi.Contract.Requests;
using TestApi.Contract.Responses;
using TestApi.DAL.Commands;
using TestApi.DAL.Commands.Core;
using TestApi.DAL.Exceptions;
using TestApi.DAL.Helpers;
using TestApi.DAL.Queries;
using TestApi.DAL.Queries.Core;
using TestApi.Domain;
using TestApi.Domain.Enums;
using TestApi.Mappings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TestApi.Common.Builders;
using TestApi.Services.Clients.UserApiClient;
using CreateUserRequest = TestApi.Contract.Requests.CreateUserRequest;

namespace TestApi.Controllers
{
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IQueryHandler _queryHandler;
        private readonly ICommandHandler _commandHandler;
        private readonly ILogger<UserController> _logger;

        public UserController(ICommandHandler commandHandler, IQueryHandler queryHandler, ILogger<UserController> logger)
        {
            _commandHandler = commandHandler;
            _queryHandler = queryHandler;
            _logger = logger;
        }

        /// <summary>
        /// Get user by user id
        /// </summary>
        /// <param name="userId">Id of the user</param>
        /// <returns>Full details of a user</returns>
        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(UserDetailsResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetUserDetailsByIdAsync(Guid userId)
        {
            _logger.LogDebug($"GetUserDetailsByIdAsync {userId}");

            var user = await GetUserByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning($"Unable to find user with id {userId}");

                return NotFound();
            }

            var response = UserToDetailsResponseMapper.MapToResponse(user);
            return Ok(response);
        }

        /// <summary>
        /// Get user by username
        /// </summary>
        /// <param name="username">Username of the user (case insensitive)</param>
        /// <returns>Full details of a user</returns>
        [HttpGet("username/{username}")]
        [ProducesResponseType(typeof(UserDetailsResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetUserDetailsByUsernameAsync(string username)
        {
            _logger.LogDebug($"GetUserDetailsByUsernameAsync {username}");

            var queriedUser = await GetUserByUsernameAsync(username);

            if (queriedUser == null)
            {
                _logger.LogWarning($"Unable to find user with username {username}");
                return NotFound();
            }

            var response = UserToDetailsResponseMapper.MapToResponse(queriedUser);
            return Ok(response);
        }

        /// <summary>
        /// Get all users details by user type and application
        /// </summary>
        /// <param name="userType">Type of user (e.g Judge)</param>
        /// <param name="application">Application (e.g. VideoWeb)</param>
        /// <returns>List of all users details for a specified application and user type</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<UserDetailsResponse>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllUsersByUserTypeAndApplicationAsync(UserType userType, Application application)
        {
            _logger.LogDebug($"GetAllUsersByUserTypeAndApplicationAsync {userType} {application}");

            var getAllUsersByUserTypeQuery = new GetAllUsersByUserTypeQuery(userType, application);
            var users = await _queryHandler.Handle<GetAllUsersByUserTypeQuery, List<User>>(getAllUsersByUserTypeQuery);
            _logger.LogDebug($"{users.Count} user(s) retrieved");

            if (users.Count.Equals(0))
            {
                return NotFound();
            }

            var usersResponse = users.Select(UserToDetailsResponseMapper.MapToResponse).ToList();

            return Ok(usersResponse);
        }

        /// <summary>
        /// Get iterated user number
        /// </summary>
        /// <param name="userType">Type of user (e.g Judge)</param>
        /// <param name="application">Application (e.g. VideoWeb)</param>
        /// <returns>The highest available user number</returns>
        [HttpGet("iterate")]
        [ProducesResponseType(typeof(IteratedUserNumberResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetHighestUserNumberByUserTypeAsync(UserType userType, Application application)
        {
            _logger.LogDebug($"GetHighestUserNumberByUserTypeAsync {userType} {application}");

            var number = await _queryHandler.Handle<GetNextUserNumberByUserTypeQuery, Integer>(new GetNextUserNumberByUserTypeQuery(userType, application));
            _logger.LogDebug($"Highest user number plus 1 will be {number}");

            var response = NumberToResponseMapper.MapToResponse(number);

            return Ok(response);
        }

        /// <summary>
        /// Create new user
        /// </summary>
        /// <param name="request">Details of the new user</param>
        /// <returns>Details of the created user</returns>
        [HttpPost]
        [ProducesResponseType(typeof(UserDetailsResponse), (int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateNewUserAsync(CreateUserRequest request)
        {
            _logger.LogDebug("CreateNewUser");

            var userId = await CreateUserAsync(request);
            _logger.LogDebug("New User Created");

            var user = await GetUserByIdAsync(userId);

            var response = UserToDetailsResponseMapper.MapToResponse(user);

            _logger.LogInformation($"Created user {response.Username} with id {response.Id}");

            return CreatedAtAction(nameof(GetUserDetailsByIdAsync), new { userId = response.Id }, response);
        }

        /// <summary>
        /// Create new AAD user
        /// </summary>
        /// <param name="request">Details of the new user</param>
        /// <returns>Details of the created user</returns>
        [HttpPost("aad")]
        [ProducesResponseType(typeof(NewUserResponse), (int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateNewADUserAsync(CreateADUserRequest request)
        {
            _logger.LogDebug("CreateNewADUser");

            var createNewAdUserCommand = new CreateNewADUserCommand
            (
                request.Title, request.FirstName, request.MiddleNames, request.LastName, request.DisplayName,
                request.Username, request.ContactEmail, request.CaseRoleName, request.HearingRoleName, request.Reference,
                request.Representee, request.OrganisationName, request.TelephoneNumber
            );

            await _commandHandler.Handle(createNewAdUserCommand);

            var user = createNewAdUserCommand.Response;
            _logger.LogDebug($"New User with username {user.Username} Created");

            return CreatedAtAction(nameof(CreateNewADUserAsync), new { userId = createNewAdUserCommand.Response.User_id }, createNewAdUserCommand.Response);
        }

        /// <summary>
        /// Delete user by user id
        /// </summary>
        /// <param name="userId">User Id of the user</param>
        /// <returns>Delete a user</returns>
        [HttpDelete]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> DeleteUserByUserIdAsync(Guid userId)
        {
            _logger.LogDebug($"DeleteUserByUserIdAsync {userId}");

            var user = await GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            await _commandHandler.Handle(new DeleteUserByUserIdCommand(userId));

            _logger.LogInformation($"Successfully deleted user with id {userId}");

            return NoContent();
        }

        private async Task<User> GetUserByIdAsync(Guid userId)
        {
            return await _queryHandler.Handle<GetUserByIdQuery, User>(new GetUserByIdQuery(userId));
        }

        private async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _queryHandler.Handle<GetUserByUsernameQuery, User>(new GetUserByUsernameQuery(username));
        }

        private async Task<Guid> CreateUserAsync(CreateUserRequest request)
        {
            var existingUser = await _queryHandler.Handle<GetUserByUsernameQuery, User>(new GetUserByUsernameQuery(request.Username));

            if (existingUser != null)
            {
                throw new UserAlreadyExistsException(existingUser.Username, existingUser.Application);
            }

            var createNewUserCommand = new CreateNewUserCommand
            (
                request.Username, request.ContactEmail, request.FirstName, request.LastName, 
                request.DisplayName, request.Number, request.UserType, request.Application
            );

            await _commandHandler.Handle(createNewUserCommand);

            return createNewUserCommand.NewUserId;
        }
    }
}
