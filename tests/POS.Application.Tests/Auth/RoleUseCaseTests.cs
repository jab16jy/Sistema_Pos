using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.Roles;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Auth;

public sealed class RoleUseCaseTests
{
    [Fact]
    public async Task DeactivateUserAsync_WhenTargetIsSoleActiveAdministrator_DeniesWithoutMutation()
    {
        var administrator = CreateAdministratorUser();
        var operatorRole = new Role("User Deactivator");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var users = new InMemoryUserRepository([administrator, requestingUser]);
        var roles = new InMemoryRoleRepository(
            [CreateAdministratorRole(), operatorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(operatorRole.Id, Set(PermissionKeys.Users.Deactivate))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeactivateUserUseCase(users, roles, unitOfWork).ExecuteAsync(administrator.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No puedes desactivar al último administrador activo.", result.Error);
        Assert.True(administrator.IsActive);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(0, users.UpdateCalls);
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenAnotherAdministratorRemains_DeactivatesTarget()
    {
        var administrator = CreateAdministratorUser();
        var secondAdministrator = new User("admin2", "hash", RoleConstants.AdministratorRoleId);
        var users = new InMemoryUserRepository([administrator, secondAdministrator]);
        var roles = new InMemoryRoleRepository([CreateAdministratorRole()]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeactivateUserUseCase(users, roles, unitOfWork).ExecuteAsync(administrator.Id, secondAdministrator.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(administrator.IsActive);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(1, users.UpdateCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenSoleAdministratorIsReassigned_DeniesWithoutChangingRole()
    {
        var administrator = CreateAdministratorUser();
        var users = new InMemoryUserRepository([administrator]);
        var roles = new InMemoryRoleRepository([CreateAdministratorRole()]);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new UpdateUserUseCase(users, roles, new FakePasswordHashService(), unitOfWork);

        var result = await useCase.ExecuteAsync(administrator.Id, null, RoleConstants.CashierRoleId, administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No puedes quitar el rol de administrador al último administrador activo.", result.Error);
        Assert.Equal(RoleConstants.AdministratorRoleId, administrator.RoleId);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(0, users.UpdateCalls);
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenTransactionConflicts_ReturnsRetryMessage()
    {
        var result = await new DeactivateUserUseCase(
                new InMemoryUserRepository([]),
                new InMemoryRoleRepository([]),
                new TransactionConflictUnitOfWork())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("La base de datos está ocupada. Inténtalo de nuevo.", result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenTransactionConflicts_ReturnsRetryMessage()
    {
        var result = await new UpdateUserUseCase(
                new InMemoryUserRepository([]),
                new InMemoryRoleRepository([]),
                new FakePasswordHashService(),
                new TransactionConflictUnitOfWork())
            .ExecuteAsync(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("La base de datos está ocupada. Inténtalo de nuevo.", result.Error);
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenTargetAdministratorIsAlreadyInactive_AllowsWithoutLastActiveAdministratorGuard()
    {
        var inactiveAdministrator = CreateAdministratorUser();
        inactiveAdministrator.Deactivate();
        var operatorRole = new Role("User Deactivator");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var users = new InMemoryUserRepository([inactiveAdministrator, requestingUser]);
        var roles = new InMemoryRoleRepository(
            [CreateAdministratorRole(), operatorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(operatorRole.Id, Set(PermissionKeys.Users.Deactivate))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeactivateUserUseCase(users, roles, unitOfWork)
            .ExecuteAsync(inactiveAdministrator.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.False(inactiveAdministrator.IsActive);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenTargetAdministratorIsAlreadyInactive_AllowsReassignmentWithoutLastActiveAdministratorGuard()
    {
        var activeAdministrator = CreateAdministratorUser();
        var inactiveAdministrator = new User("inactive-admin", "hash", RoleConstants.AdministratorRoleId);
        inactiveAdministrator.Deactivate();
        var users = new InMemoryUserRepository([activeAdministrator, inactiveAdministrator]);
        var roles = new InMemoryRoleRepository([CreateAdministratorRole(), CreateCashierRole()]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new UpdateUserUseCase(users, roles, new FakePasswordHashService(), unitOfWork)
            .ExecuteAsync(
                inactiveAdministrator.Id,
                null,
                RoleConstants.CashierRoleId,
                activeAdministrator.Id,
                TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Equal(RoleConstants.CashierRoleId, inactiveAdministrator.RoleId);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenAdministratorPermissionsChange_DeniesAndKeepsEmptyGrantSet()
    {
        var administrator = CreateAdministratorUser();
        var roles = new InMemoryRoleRepository([CreateAdministratorRole()]);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new UpdateRoleUseCase(new InMemoryUserRepository([administrator]), roles, unitOfWork);

        var result = await useCase.ExecuteAsync(
            RoleConstants.AdministratorRoleId,
            RoleConstants.AdministratorRoleName,
            Set(PermissionKeys.Reports.View),
            administrator.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Los permisos del rol Administrador no se pueden modificar.", result.Error);
        Assert.Empty(await roles.GetPermissionKeysAsync(RoleConstants.AdministratorRoleId, TestContext.Current.CancellationToken));
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateRoleAsync_WithUnchangedEmptyGrantSet_AllowsRenameWithRolesEditOnly()
    {
        var role = new Role("Empty Role");
        var editorRole = new Role("Role Editor");
        var requestingUser = new User("editor", "hash", editorRole.Id);
        var roles = new InMemoryRoleRepository(
            [role, editorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(editorRole.Id, Set(PermissionKeys.Roles.Edit))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new UpdateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync(role.Id, "Renamed Empty Role", Set(), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Renamed Empty Role", role.Name);
        Assert.Equal(0, roles.ReplacePermissionsCalls);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateRoleAsync_WithIdenticalNonEmptyGrantSet_AllowsRenameWithRolesEditOnly()
    {
        var role = new Role("Report Role");
        var editorRole = new Role("Role Editor");
        var requestingUser = new User("editor", "hash", editorRole.Id);
        var grants = Set(PermissionKeys.Reports.View);
        var roles = new InMemoryRoleRepository(
            [role, editorRole],
            [
                new KeyValuePair<Guid, IReadOnlySet<string>>(role.Id, grants),
                new KeyValuePair<Guid, IReadOnlySet<string>>(editorRole.Id, Set(PermissionKeys.Roles.Edit))
            ]);

        var result = await new UpdateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, new CountingUnitOfWork())
            .ExecuteAsync(role.Id, "Renamed Report Role", Set(PermissionKeys.Reports.View), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Renamed Report Role", role.Name);
        Assert.Equal(0, roles.ReplacePermissionsCalls);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenGrantSetChangesWithoutRolesAssignPermissions_Denies()
    {
        var role = new Role("Report Role");
        var editorRole = new Role("Role Editor");
        var requestingUser = new User("editor", "hash", editorRole.Id);
        var roles = new InMemoryRoleRepository(
            [role, editorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(editorRole.Id, Set(PermissionKeys.Roles.Edit))]);

        var result = await new UpdateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, new CountingUnitOfWork())
            .ExecuteAsync(role.Id, role.Name, Set(PermissionKeys.Reports.View), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No tienes permiso para asignar permisos a roles.", result.Error);
        Assert.Empty(await roles.GetPermissionKeysAsync(role.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenGrantSetChanges_PersistsAssignmentAndRevocation()
    {
        var role = new Role("Report Role");
        var administrator = CreateAdministratorUser();
        var roles = new InMemoryRoleRepository([role, CreateAdministratorRole()]);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new UpdateRoleUseCase(new InMemoryUserRepository([administrator]), roles, unitOfWork);

        var assigned = await useCase.ExecuteAsync(role.Id, role.Name, Set(PermissionKeys.Reports.View), administrator.Id, TestContext.Current.CancellationToken);
        Assert.Contains(PermissionKeys.Reports.View, await roles.GetPermissionKeysAsync(role.Id, TestContext.Current.CancellationToken));
        var revoked = await useCase.ExecuteAsync(role.Id, role.Name, Set(), administrator.Id, TestContext.Current.CancellationToken);

        Assert.True(assigned.Success);
        Assert.True(revoked.Success);
        Assert.Empty(await roles.GetPermissionKeysAsync(role.Id, TestContext.Current.CancellationToken));
        Assert.Equal(2, roles.ReplacePermissionsCalls);
        Assert.Equal(2, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateRoleAsync_WhenNameExists_Denies()
    {
        var existingRole = new Role("Report Viewer");
        var administrator = CreateAdministratorUser();
        var roles = new InMemoryRoleRepository([existingRole, CreateAdministratorRole()]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateRoleUseCase(new InMemoryUserRepository([administrator]), roles, unitOfWork)
            .ExecuteAsync("Report Viewer", Set(), administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Ya existe un rol con ese nombre.", result.Error);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateRoleAsync_WhenPermissionKeyIsUnknown_Denies()
    {
        var administrator = CreateAdministratorUser();
        var roles = new InMemoryRoleRepository([CreateAdministratorRole()]);

        var result = await new CreateRoleUseCase(new InMemoryUserRepository([administrator]), roles, new CountingUnitOfWork())
            .ExecuteAsync("Unknown Permission Role", Set("Unknown.Permission"), administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Uno o más permisos no son válidos.", result.Error);
    }

    [Fact]
    public async Task CreateRoleAsync_WithRolesCreateOnlyAndNoPermissions_CreatesEmptyRole()
    {
        var creatorRole = new Role("Role Creator");
        var requestingUser = new User("creator", "hash", creatorRole.Id);
        var roles = new InMemoryRoleRepository(
            [creatorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(creatorRole.Id, Set(PermissionKeys.Roles.Create))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync("Empty Role", Set(), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Role createdRole = Assert.IsType<Role>(await roles.GetByNameAsync("Empty Role", TestContext.Current.CancellationToken));
        Assert.Empty(await roles.GetPermissionKeysAsync(createdRole.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateRoleAsync_WithRolesCreateOnlyAndPermissionGrants_DeniesWithoutMutationOrSave()
    {
        var creatorRole = new Role("Role Creator");
        var requestingUser = new User("creator", "hash", creatorRole.Id);
        var roles = new InMemoryRoleRepository(
            [creatorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(creatorRole.Id, Set(PermissionKeys.Roles.Create))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync("Privileged Role", Set(PermissionKeys.Users.Edit), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No tienes permiso para asignar permisos a roles.", result.Error);
        Assert.Null(await roles.GetByNameAsync("Privileged Role", TestContext.Current.CancellationToken));
        Assert.Equal(0, roles.ReplacePermissionsCalls);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateRoleAsync_WithRolesCreateAndAssignPermissions_CreatesRolePersistsGrantsAndSavesOnce()
    {
        var managerRole = new Role("Role Manager");
        var requestingUser = new User("manager", "hash", managerRole.Id);
        var roles = new InMemoryRoleRepository(
            [managerRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(
                managerRole.Id,
                Set(PermissionKeys.Roles.Create, PermissionKeys.Roles.AssignPermissions))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync("Report Viewer", Set(PermissionKeys.Reports.View), requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Role createdRole = Assert.IsType<Role>(await roles.GetByNameAsync("Report Viewer", TestContext.Current.CancellationToken));
        Assert.True((await roles.GetPermissionKeysAsync(createdRole.Id, TestContext.Current.CancellationToken))
            .SetEquals(Set(PermissionKeys.Reports.View)));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateUserAsync_WithoutRolesAssignPermissions_DeniesCustomRoleAssignmentWithoutMutationOrSave()
    {
        var operatorRole = new Role("User Manager");
        var elevatedRole = new Role("Elevated Role");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var users = new InMemoryUserRepository([requestingUser]);
        var roles = new InMemoryRoleRepository(
            [operatorRole, elevatedRole],
            [
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    operatorRole.Id,
                    Set(PermissionKeys.Users.Create, PermissionKeys.Users.Edit)),
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    elevatedRole.Id,
                    Set(PermissionKeys.Users.Deactivate, PermissionKeys.Roles.AssignPermissions))
            ]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateUserUseCase(users, roles, new FakePasswordHashService(), unitOfWork)
            .ExecuteAsync("controlled", "1234", elevatedRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No tienes permiso para asignar roles.", result.Error);
        Assert.Null(await users.GetByUsernameAsync("controlled", TestContext.Current.CancellationToken));
        Assert.Equal(0, users.AddCalls);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateUserAsync_WithRolesAssignPermissions_AllowsCustomRoleAssignment()
    {
        var operatorRole = new Role("User Manager");
        var elevatedRole = new Role("Elevated Role");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var users = new InMemoryUserRepository([requestingUser]);
        var roles = new InMemoryRoleRepository(
            [operatorRole, elevatedRole],
            [
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    operatorRole.Id,
                    Set(PermissionKeys.Users.Create, PermissionKeys.Users.Edit, PermissionKeys.Roles.AssignPermissions)),
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    elevatedRole.Id,
                    Set(PermissionKeys.Users.Deactivate, PermissionKeys.Roles.AssignPermissions))
            ]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new CreateUserUseCase(users, roles, new FakePasswordHashService(), unitOfWork)
            .ExecuteAsync("controlled", "1234", elevatedRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        User createdUser = Assert.IsType<User>(await users.GetByUsernameAsync("controlled", TestContext.Current.CancellationToken));
        Assert.Equal(elevatedRole.Id, createdUser.RoleId);
        Assert.Equal(1, users.AddCalls);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateUserAsync_WithoutRolesAssignPermissions_DeniesCustomRoleAssignmentWithoutMutation()
    {
        var operatorRole = new Role("User Manager");
        var controlledRole = new Role("Controlled Role");
        var elevatedRole = new Role("Elevated Role");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var controlledUser = new User("controlled", "hash:original", controlledRole.Id);
        var users = new InMemoryUserRepository([requestingUser, controlledUser]);
        var roles = new InMemoryRoleRepository(
            [operatorRole, controlledRole, elevatedRole],
            [
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    operatorRole.Id,
                    Set(PermissionKeys.Users.Create, PermissionKeys.Users.Edit)),
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    elevatedRole.Id,
                    Set(PermissionKeys.Users.Deactivate, PermissionKeys.Roles.AssignPermissions))
            ]);

        var result = await new UpdateUserUseCase(users, roles, new FakePasswordHashService(), new CountingUnitOfWork())
            .ExecuteAsync(controlledUser.Id, "updated-password", elevatedRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No tienes permiso para asignar roles.", result.Error);
        Assert.Equal(controlledRole.Id, controlledUser.RoleId);
        Assert.Equal("hash:original", controlledUser.PasswordHash);
        Assert.Equal(0, users.UpdateCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_WithRolesAssignPermissions_AllowsCustomRoleAssignment()
    {
        var operatorRole = new Role("User Manager");
        var controlledRole = new Role("Controlled Role");
        var elevatedRole = new Role("Elevated Role");
        var requestingUser = new User("operator", "hash", operatorRole.Id);
        var controlledUser = new User("controlled", "hash:original", controlledRole.Id);
        var users = new InMemoryUserRepository([requestingUser, controlledUser]);
        var roles = new InMemoryRoleRepository(
            [operatorRole, controlledRole, elevatedRole],
            [
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    operatorRole.Id,
                    Set(PermissionKeys.Users.Create, PermissionKeys.Users.Edit, PermissionKeys.Roles.AssignPermissions)),
                new KeyValuePair<Guid, IReadOnlySet<string>>(
                    elevatedRole.Id,
                    Set(PermissionKeys.Users.Deactivate, PermissionKeys.Roles.AssignPermissions))
            ]);

        var unitOfWork = new CountingUnitOfWork();
        var result = await new UpdateUserUseCase(users, roles, new FakePasswordHashService(), unitOfWork)
            .ExecuteAsync(controlledUser.Id, "updated-password", elevatedRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Equal(elevatedRole.Id, controlledUser.RoleId);
        Assert.Equal("hash:updated-password", controlledUser.PasswordHash);
        Assert.Equal(1, users.UpdateCalls);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task DeactivateRoleAsync_WhenRoleIsSystem_Denies()
    {
        var administrator = CreateAdministratorUser();
        var roles = new InMemoryRoleRepository([CreateAdministratorRole(), CreateCashierRole()]);
        var useCase = new DeactivateRoleUseCase(new InMemoryUserRepository([administrator]), roles, new CountingUnitOfWork());

        var administratorResult = await useCase.ExecuteAsync(RoleConstants.AdministratorRoleId, administrator.Id, TestContext.Current.CancellationToken);
        var cashierResult = await useCase.ExecuteAsync(RoleConstants.CashierRoleId, administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(administratorResult.Success);
        Assert.False(cashierResult.Success);
        Assert.Equal("Los roles del sistema no se pueden desactivar.", administratorResult.Error);
        Assert.Equal("Los roles del sistema no se pueden desactivar.", cashierResult.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeactivateRoleAsync_WhenCustomRoleHasActiveOrInactiveAssignedUser_Denies(bool assignedUserIsActive)
    {
        var role = new Role("Assigned Role");
        var administrator = CreateAdministratorUser();
        var assignedUser = new User("assigned", "hash", role.Id);
        if (!assignedUserIsActive)
            assignedUser.Deactivate();
        var roles = new InMemoryRoleRepository([role, CreateAdministratorRole()], assignedUsers: [assignedUser]);

        var result = await new DeactivateRoleUseCase(new InMemoryUserRepository([administrator]), roles, new CountingUnitOfWork())
            .ExecuteAsync(role.Id, administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("No puedes desactivar un rol asignado a usuarios.", result.Error);
        Assert.True(role.IsActive);
    }

    [Fact]
    public async Task DeactivateRoleAsync_WhenRoleIsUnassigned_DeactivatesRole()
    {
        var role = new Role("Unused Role");
        var administrator = CreateAdministratorUser();
        var unitOfWork = new CountingUnitOfWork();
        var roles = new InMemoryRoleRepository([role, CreateAdministratorRole()]);

        var result = await new DeactivateRoleUseCase(new InMemoryUserRepository([administrator]), roles, unitOfWork)
            .ExecuteAsync(role.Id, administrator.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(role.IsActive);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivateRoleAsync_WithActiveCustomRoleGrantedRolesDeactivate_DeactivatesUnassignedRole()
    {
        var targetRole = new Role("Unused Role");
        var deactivatorRole = new Role("Role Deactivator");
        var requestingUser = new User("deactivator", "hash", deactivatorRole.Id);
        var roles = new InMemoryRoleRepository(
            [targetRole, deactivatorRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(
                deactivatorRole.Id,
                Set(PermissionKeys.Roles.Deactivate))]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeactivateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync(targetRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(RoleConstants.AdministratorRoleId, deactivatorRole.Id);
        Assert.True(deactivatorRole.IsActive);
        Assert.True(result.Success, result.Error);
        Assert.False(targetRole.IsActive);
        Assert.Equal(1, roles.UpdateCalls);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivateRoleAsync_WithoutRolesDeactivate_DeniesWithoutMutatingTargetOrSaving()
    {
        var targetRole = new Role("Unused Role");
        var unprivilegedRole = new Role("Role Viewer");
        var requestingUser = new User("viewer", "hash", unprivilegedRole.Id);
        var roles = new InMemoryRoleRepository([targetRole, unprivilegedRole]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeactivateRoleUseCase(new InMemoryUserRepository([requestingUser]), roles, unitOfWork)
            .ExecuteAsync(targetRole.Id, requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.True(unprivilegedRole.IsActive);
        Assert.False(result.Success);
        Assert.True(targetRole.IsActive);
        Assert.Equal(unprivilegedRole.Id, Assert.Single(roles.GetByIdRoleIds));
        Assert.Equal(0, roles.UpdateCalls);
        Assert.Equal(0, roles.CountUsersInRoleCalls);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsSystemAndInactiveRoles()
    {
        var administrator = CreateAdministratorUser();
        var inactiveRole = new Role("Inactive Role");
        inactiveRole.Deactivate();
        var roles = new InMemoryRoleRepository([CreateAdministratorRole(), inactiveRole]);

        IReadOnlyList<RoleSummary> result = await new GetAllRolesUseCase(new InMemoryUserRepository([administrator]), roles)
            .ExecuteAsync(administrator.Id, TestContext.Current.CancellationToken);

        Assert.Contains(result, role => role.IsSystem && role.Id == RoleConstants.AdministratorRoleId);
        Assert.Contains(result, role => !role.IsActive && role.Id == inactiveRole.Id);
    }

    [Fact]
    public async Task GetAllRolesAsync_WithActiveCustomRoleGrantedRolesView_ReturnsRoles()
    {
        var viewerRole = new Role("Role Viewer");
        var requestingUser = new User("viewer", "hash", viewerRole.Id);
        var listedRole = new Role("Inventory Viewer");
        var roles = new InMemoryRoleRepository(
            [viewerRole, listedRole],
            [new KeyValuePair<Guid, IReadOnlySet<string>>(
                viewerRole.Id,
                Set(PermissionKeys.Roles.View))]);

        IReadOnlyList<RoleSummary> result = await new GetAllRolesUseCase(new InMemoryUserRepository([requestingUser]), roles)
            .ExecuteAsync(requestingUser.Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(RoleConstants.AdministratorRoleId, viewerRole.Id);
        Assert.True(viewerRole.IsActive);
        Assert.Contains(result, role => role.Id == listedRole.Id);
        Assert.Equal(1, roles.GetAllCalls);
    }

    [Fact]
    public async Task GetAllRolesAsync_WithoutRolesView_ThrowsAndDoesNotEnumerateRoles()
    {
        var unprivilegedRole = new Role("Role Deactivator");
        var requestingUser = new User("deactivator", "hash", unprivilegedRole.Id);
        var roles = new InMemoryRoleRepository([unprivilegedRole, new Role("Inventory Viewer")]);

        await Assert.ThrowsAsync<UnauthorizedActionException>(() =>
            new GetAllRolesUseCase(new InMemoryUserRepository([requestingUser]), roles)
                .ExecuteAsync(requestingUser.Id, TestContext.Current.CancellationToken));

        Assert.True(unprivilegedRole.IsActive);
        Assert.Equal(0, roles.GetAllCalls);
    }

    private static User CreateAdministratorUser() => new("admin", "hash", RoleConstants.AdministratorRoleId);

    private static Role CreateAdministratorRole() => Role.CreateSystemRole(
        RoleConstants.AdministratorRoleId,
        RoleConstants.AdministratorRoleName);

    private static Role CreateCashierRole() => Role.CreateSystemRole(
        RoleConstants.CashierRoleId,
        RoleConstants.CashierRoleName);

    private static IReadOnlySet<string> Set(params string[] keys) => new HashSet<string>(keys, StringComparer.Ordinal);

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";
        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            await operation(cancellationToken);
        }
    }

    private sealed class TransactionConflictUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
            => Task.FromException(new TransactionConflictException(new InvalidOperationException()));
    }

    private sealed class InMemoryUserRepository(IEnumerable<User> users) : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users = users.ToDictionary(user => user.Id);

        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.GetValueOrDefault(id));

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.Values.FirstOrDefault(user => user.Username == username));

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_users.Count > 0);
        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            _users.Add(user.Id, user);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_users.Values.Count(user => user.IsActive && user.RoleId == roleId));
    }

    private sealed class InMemoryRoleRepository : IRoleRepository
    {
        private readonly Dictionary<Guid, Role> _roles;
        private readonly Dictionary<Guid, HashSet<string>> _grants;
        private readonly IReadOnlyCollection<User> _assignedUsers;
        private readonly List<Guid> _getByIdRoleIds = [];

        public InMemoryRoleRepository(
            IEnumerable<Role> roles,
            IEnumerable<KeyValuePair<Guid, IReadOnlySet<string>>>? grants = null,
            IEnumerable<User>? assignedUsers = null)
        {
            _roles = roles.ToDictionary(role => role.Id);
            _grants = grants?.ToDictionary(
                grant => grant.Key,
                grant => new HashSet<string>(grant.Value, StringComparer.Ordinal))
                ?? [];
            _assignedUsers = assignedUsers?.ToArray() ?? [];
        }

        public int ReplacePermissionsCalls { get; private set; }
        public int GetAllCalls { get; private set; }
        public int GetByIdCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int CountUsersInRoleCalls { get; private set; }
        public IReadOnlyList<Guid> GetByIdRoleIds => _getByIdRoleIds;

        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            GetByIdCalls++;
            _getByIdRoleIds.Add(id);
            return Task.FromResult(_roles.GetValueOrDefault(id));
        }
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(_roles.Values.FirstOrDefault(role => role.Name == name));
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
        {
            GetAllCalls++;
            return Task.FromResult<IReadOnlyList<Role>>(_roles.Values.OrderBy(role => role.Name).ToList());
        }
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(_grants.GetValueOrDefault(roleId)?.Contains(permissionKey) == true);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(_grants.GetValueOrDefault(roleId) ?? [], StringComparer.Ordinal));
        public Task AddAsync(Role role, CancellationToken ct = default) { _roles.Add(role.Id, role); return Task.CompletedTask; }
        public Task UpdateAsync(Role role, CancellationToken ct = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default)
        {
            ReplacePermissionsCalls++;
            _grants[roleId] = new HashSet<string>(keys, StringComparer.Ordinal);
            return Task.CompletedTask;
        }

        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
        {
            CountUsersInRoleCalls++;
            return Task.FromResult(_assignedUsers.Count(user => user.RoleId == roleId));
        }

        public int DeleteCalls { get; private set; }
        public Task DeleteAsync(Role role, CancellationToken ct = default)
        {
            DeleteCalls++;
            _roles.Remove(role.Id);
            _grants.Remove(role.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DeleteRoleAsync_SystemRole_ReturnsError()
    {
        var adminRole = CreateAdministratorRole();
        var adminUser = CreateAdministratorUser();
        var users = new InMemoryUserRepository([adminUser]);
        var roles = new InMemoryRoleRepository([adminRole]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeleteRoleUseCase(users, roles, unitOfWork).ExecuteAsync(adminRole.Id, adminUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("sistema", result.Error);
        Assert.Equal(0, roles.DeleteCalls);
    }

    [Fact]
    public async Task DeleteRoleAsync_RoleWithUsers_ReturnsError()
    {
        var customRole = new Role("Custom");
        var adminUser = CreateAdministratorUser();
        var userInRole = new User("user1", "hash", customRole.Id);
        var users = new InMemoryUserRepository([adminUser, userInRole]);
        var roles = new InMemoryRoleRepository([CreateAdministratorRole(), customRole], assignedUsers: [userInRole]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeleteRoleUseCase(users, roles, unitOfWork).ExecuteAsync(customRole.Id, adminUser.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("asignado a usuarios", result.Error);
        Assert.Equal(0, roles.DeleteCalls);
    }

    [Fact]
    public async Task DeleteRoleAsync_ValidCustomRole_DeletesSuccessfully()
    {
        var customRole = new Role("Unused Custom");
        var adminUser = CreateAdministratorUser();
        var users = new InMemoryUserRepository([adminUser]);
        var roles = new InMemoryRoleRepository([CreateAdministratorRole(), customRole]);
        var unitOfWork = new CountingUnitOfWork();

        var result = await new DeleteRoleUseCase(users, roles, unitOfWork).ExecuteAsync(customRole.Id, adminUser.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, roles.DeleteCalls);
        Assert.Equal(1, unitOfWork.SaveCount);
    }
}
