//using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CICD;

public partial interface IDataAccess
{
    Task<DataObjects.User> CreateNewUserFromEmailAddress(Guid TenantId, string EmailAddress);
    Task<DataObjects.BooleanResponse> DeleteUser(Guid UserId, DataObjects.User? CurrentUser = null, bool ForceDeleteImmediately = false);
    Task<DataObjects.BooleanResponse> DeleteUserPhoto(Guid UserId);
    string DisplayNameFromLastAndFirst(string? LastName, string? FirstName, string? Email);
    Task<DataObjects.User> ForgotPassword(DataObjects.User user);
    Task<DataObjects.User> ForgotPasswordConfirm(DataObjects.User user);
    Task<DataObjects.ActiveUser> GetActiveUser(Guid userId);
    Task<List<DataObjects.ActiveUser>> GetActiveUsers(DataObjects.User CurrentUser);
    Task<string> GetDisplayNameFromUserId(Guid? UserId, bool LastNameFirst = false, DataObjects.User? CurrentUser = null);
    Task<string> GetEmailFromUserId(Guid? UserId, DataObjects.User? CurrentUser = null);
    Task<string> GetFirstNameFromUserId(Guid? UserId);
    Task<DataObjects.User> GetUser(Guid TenantId, string UserName);
    Task<DataObjects.User> GetUser(Guid UserId, bool ValidateMainAdminAccess = false, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.UserAccount>> GetUserAccounts(string? email);
    Task<DataObjects.User> GetUserByEmailAddress(Guid TenantId, string EmailAddress, bool AddIfNotFound = true);
    Task<DataObjects.User> GetUserByEmployeeId(Guid TenantId, string EmployeeId, bool AddIfNotFound = false);
    Task<DataObjects.User> GetUserByUsername(Guid TenantId, string Username, bool AddIfNotFound = false);
    Task<DataObjects.User> GetUserByUsernameOrEmail(Guid TenantId, string search, bool AddIfNotFound = true);
    Task<string> GetUserDisplayName(Guid? UserId);
    Task<DataObjects.FilterUsers> GetUsersFiltered(DataObjects.FilterUsers filter, DataObjects.User? CurrentUser = null);
    Task<DataObjects.User> GetUserFromToken(string? Token, string fingerprint = "");
    Task<DataObjects.User> GetUserFromToken(Guid TenantId, string? Token, string fingerprint = "");
    Task<List<DataObjects.UserListing>> GetUserListing(Guid TenantId);
    Task<Guid?> GetUserPhoto(Guid UserId);
    Task<DataObjects.SimpleResponse> GetUserPhotoId(Guid UserId);
    Task<List<DataObjects.User>> GetUsers(Guid TenantId);
    Task<List<DataObjects.User>> GetUsersForEmailAddress(string? EmailAddress, string fingerprint = "");
    Task<List<DataObjects.UserTenant>> GetUserTenantList(string? username, string? email, bool enabledUsersOnly = true);
    Task<List<DataObjects.Tenant>?> GetUserTenants(string? username, string? email, bool enabledUsersOnly = true);
    string GetUserToken(Guid TenantId, Guid UserId, string fingerprint = "");
    string LastModifiedDisplayName(string? lastModifiedBy);
    string LastModifiedDisplayName(DataObjects.User? CurrentUser);
    Task<DataObjects.BooleanResponse> ResetUserPassword(DataObjects.UserPasswordReset reset, DataObjects.User currentUser);
    Task<DataObjects.User> SaveUser(DataObjects.User user, DataObjects.User? CurrentUser = null);
    Task<DataObjects.User> SaveUserByUsername(DataObjects.User user, bool CreateIfNotFound, DataObjects.User? CurrentUser = null);
    Task<DataObjects.BooleanResponse> SaveUserPreferences(Guid UserId, DataObjects.UserPreferences userPreferences);
    Task<List<DataObjects.User>> SaveUsers(List<DataObjects.User> users, DataObjects.User? CurrentUser = null);
    Task<DataObjects.User> UnlockUserAccount(Guid UserId, DataObjects.User? CurrentUser = null);
    //Task<DataObjects.User?> UpdateUserFromExternalDataSources(DataObjects.User User, DataObjects.TenantSettings? settings = null);
    Task UpdateUserLastLoginTime(Guid UserId, string? Source = "");
    Task<bool> UserCanEditUser(Guid UserId, Guid EditUserId);
    Task<bool> UserCanViewUser(Guid UserId, Guid ViewUserId);
    Task<bool> UserIsMainAdmin(Guid UserId);
    Task<DataObjects.User> UserSignup(DataObjects.User user);
    Task<DataObjects.User> UserSignupConfirm(DataObjects.User user);
    Task<DataObjects.BooleanResponse> ValidateSelectedUserAccount(Guid TenantId, Guid UserId, DataObjects.User? CurrentUser = null);
}

public partial class DataAccess
{
    private DataObjects.User CopyUser(DataObjects.User user)
    {
        DataObjects.User output = new DataObjects.User();
        var dup = DuplicateObject<DataObjects.User>(user);
        if (dup != null) {
            output = dup;
        }
        return output;
    }

    public async Task<DataObjects.User> CreateNewUserFromEmailAddress(Guid TenantId, string EmailAddress)
    {
        DataObjects.User output = new DataObjects.User();
        // First, make sure the user doesn't already exist

        var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Email != null && x.Email.ToLower() == EmailAddress.ToLower());
        if (rec != null) {
            // Already exists
            output = await GetUser(rec.UserId);

            if (BooleanValue(rec.Deleted) == true) {
                output.ActionResponse.Messages.Add("User Already Exists!");
            } else {
                output.ActionResponse.Messages.Add("User Already Exists");
            }
            
            return output;
        }

        DateTime now = DateTime.UtcNow;

        // Create a new user record
        Guid UserId = Guid.NewGuid();
        rec = new EFModels.EFModels.User();
        rec.Added = now;
        rec.Admin = false;
        rec.Deleted = false;
        rec.Email = EmailAddress;
        rec.Enabled = true;
        rec.FirstName = "";
        rec.LastName = EmailAddress;
        rec.LastModified = now;
        rec.PreventPasswordChange = false;
        rec.ManageFiles = output.ManageFiles;
        rec.TenantId = TenantId;
        rec.UserId = UserId;
        rec.Username = EmailAddress;
        try {
            data.Users.Add(rec);
            await data.SaveChangesAsync();
            output = await GetUser(UserId);
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("There was an error adding a new user for " + EmailAddress + ":");
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> DeleteUser(Guid UserId, DataObjects.User? CurrentUser = null, bool ForceDeleteImmediately = false)
    {
        DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();

        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId);
        if (rec == null) {
            output.Messages.Add("Error Deleting User " + UserId.ToString() + " - Record No Longer Exists");
            return output;
        } else {
            var now = DateTime.UtcNow;
            Guid tenantId = GuidValue(rec.TenantId);
            var tenantSettings = GetTenantSettings(tenantId);

            // First, fix or delete all relational user records
            if(ForceDeleteImmediately || tenantSettings.DeletePreference == DataObjects.DeletePreference.Immediate) {
                try {
                    // Update any LastModifiedBy values for this UserId to be the Display Name
                    string displayName = MaxStringLength(rec.FirstName + " " + rec.LastName, 100);

                    
                    await data.Database.ExecuteSqlRawAsync("UPDATE FileStorage SET LastModifiedBy={0} WHERE LastModifiedBy={1}", displayName, UserId.ToString());
                    await data.Database.ExecuteSqlRawAsync("UPDATE FileStorage SET UploadedBy={0} WHERE UploadedBy={1}", displayName, UserId.ToString());
                    
                    await data.Database.ExecuteSqlRawAsync("UPDATE Services SET LastModifiedBy={0} WHERE LastModifiedBy={1}", displayName, UserId.ToString());
                    await data.Database.ExecuteSqlRawAsync("UPDATE Services SET AddedBy={0} WHERE AddedBy={1}", displayName, UserId.ToString());

                    await data.Database.ExecuteSqlRawAsync("UPDATE Settings SET LastModifiedBy={0} WHERE LastModifiedBy={1}", displayName, UserId.ToString());
                    
                    await data.Database.ExecuteSqlRawAsync("UPDATE Tenants SET LastModifiedBy={0} WHERE LastModifiedBy={1}", displayName, UserId.ToString());
                    await data.Database.ExecuteSqlRawAsync("UPDATE Tenants SET AddedBy={0} WHERE AddedBy={1}", displayName, UserId.ToString());

                    await data.Database.ExecuteSqlRawAsync("UPDATE Users SET LastModifiedBy={0} WHERE LastModifiedBy={1}", displayName, UserId.ToString());
                    await data.Database.ExecuteSqlRawAsync("UPDATE Users SET AddedBy={0} WHERE AddedBy={1}", displayName, UserId.ToString());

                    data.FileStorages.RemoveRange(data.FileStorages.Where(x => x.UserId == UserId));
                    data.Settings.RemoveRange(data.Settings.Where(x => x.UserId == UserId));

                    await data.SaveChangesAsync();
                } catch (Exception ex) {
                    output.Messages.Add("Error Deleting Related User Records for User " + UserId.ToString() + ":");
                    output.Messages.AddRange(RecurseException(ex));
                    return output;
                }
            }
            

            // Now, delete the main user record
            if (ForceDeleteImmediately ||tenantSettings.DeletePreference == DataObjects.DeletePreference.Immediate) {
                data.Users.Remove(rec);
            } else {

                rec.Deleted = true;
                rec.DeletedAt = now;
                rec.LastModified = now;
                if(CurrentUser != null) {
                    rec.LastModifiedBy = CurrentUserIdString(CurrentUser);
                }
            }

            try {
                await data.SaveChangesAsync();
                output.Result = true;

                if (!ForceDeleteImmediately) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        TenantId = tenantId,
                        ItemId = UserId,
                        UpdateType = DataObjects.SignalRUpdateType.User,
                        Message = "Deleted",
                        UserId = CurrentUserId(CurrentUser),
                    });
                }
            } catch (Exception ex) {
                output.Messages.Add("Error Deleting User " + UserId.ToString() + ":");
                output.Messages.AddRange(RecurseException(ex));
            }
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> DeleteUserPhoto(Guid UserId)
    {
        DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();

        var rec = await data.FileStorages.FirstOrDefaultAsync(x => x.ItemId == null && x.UserId == UserId);
        if (rec != null) {
            data.FileStorages.Remove(rec);
            Guid tenantId = GuidValue(rec.TenantId);
            try {
                await data.SaveChangesAsync();
                output.Result = true;

                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    TenantId = tenantId,
                    ItemId = UserId,
                    UpdateType = DataObjects.SignalRUpdateType.User,
                    Message = "DeletedUserPhoto"
                });
            } catch (Exception ex) {
                output.Messages.Add("Error Deleting Photo for UserId " + UserId.ToString() + ":");
                output.Messages.AddRange(RecurseException(ex));
            }
        } else {
            output.Messages.Add("File '" + UserId.ToString() + "' no longer exists");
        }

        return output;
    }

    public string DisplayNameFromLastAndFirst(string? LastName, string? FirstName, string? Email)
    {
        string output = String.Empty;

        if (!String.IsNullOrEmpty(FirstName)) {
            output += FirstName;
        }

        if (!String.IsNullOrEmpty(LastName)) {
            if (!String.IsNullOrEmpty(output)) {
                output += " ";
            }
            output += LastName;
        }

        if (String.IsNullOrEmpty(output) && !String.IsNullOrEmpty(Email)) {
            output = Email;
        }

        return output;
    }

    public async Task<DataObjects.User> ForgotPassword(DataObjects.User user)
    {
        DataObjects.User output = new DataObjects.User();
        output.ActionResponse = GetNewActionResponse();

        var applicationURL = ApplicationURL;

        if (String.IsNullOrWhiteSpace(applicationURL)) {
            output.ActionResponse.Messages.Add("Unable to Determine Referring Website Address");
        }

        if (String.IsNullOrWhiteSpace(user.Email)) {
            output.ActionResponse.Messages.Add("Missing Required Email Address");
        } else if (!user.Email.IsEmailAddress()) {
            output.ActionResponse.Messages.Add("Invalid Email Address");
        }

        if (String.IsNullOrWhiteSpace(user.Password)) {
            output.ActionResponse.Messages.Add("Missing Required Password");
        }

        if (output.ActionResponse.Messages.Count() == 0) {
            // Make sure this username is a valid existing user
            var existing = await GetUser(user.TenantId, StringValue(user.Email));
            if (existing == null || !existing.ActionResponse.Result) {
                output.ActionResponse.Messages.Add("The email address '" + user.Email + "' is not a valid local account.");
                return output;
            }

            // Make sure the user account is enabled
            if (!existing.Enabled) {
                output.ActionResponse.Messages.Add("Your account has been disabled by an admin.");
                return output;
            }

            // Make sure this user doesn't have the flag set to prevent changing password
            if (existing.PreventPasswordChange) {
                output.ActionResponse.Messages.Add("Your account has been restricted by an admin to prevent password changes.");
                return output;
            }

            string websiteName = WebsiteName(applicationURL);
            if (String.IsNullOrWhiteSpace(websiteName)) {
                websiteName += applicationURL;
            }

            string code = GenerateRandomCode(6);

            string body = "<p>You are receiving this email because you used the Forgot Password option at <strong>" + websiteName + "</strong>.</p>" +
                    "<p>Use the following confirmation code on that page to confirm your new password:</p>" +
                    "<p style='font-size:2em;'>" + code + "</p>";

            List<string> to = new List<string>();
            to.Add(StringValue(user.Email));

            var settings = GetTenantSettings(user.TenantId);

            string from = String.Empty;
            if (settings != null) {
                from += settings.DefaultReplyToAddress;
            }

            var sent = SendEmail(new DataObjects.EmailMessage {
                From = from,
                To = to,
                Subject = "Forgot Password at " + websiteName,
                Body = body
            });

            if (sent.Result) {
                output = new DataObjects.User {
                    ActionResponse = GetNewActionResponse(true),
                    UserId = existing.UserId,
                    TenantId = existing.TenantId,
                    FirstName = existing.FirstName,
                    LastName = existing.LastName,
                    Email = existing.Email,
                    Username = StringValue(existing.Email),
                    Password = user.Password,
                    AuthToken = CompressByteArrayString(Encrypt(code))
                };
            } else {
                output.ActionResponse.Messages.Add("There was an error sending an email to the address you specified.");
            }
        }

        return output;
    }

    public async Task<DataObjects.User> ForgotPasswordConfirm(DataObjects.User user)
    {
        DataObjects.User output = new DataObjects.User();

        if (String.IsNullOrWhiteSpace(user.Email)) {
            output.ActionResponse.Messages.Add("Missing Required Email Address");
        }

        if (String.IsNullOrWhiteSpace(user.Password)) {
            output.ActionResponse.Messages.Add("Missing Required Password");
        }

        if (output.ActionResponse.Messages.Count() == 0) {
            string extended = CompressedByteArrayStringToFullString(user.AuthToken);
            string decrypted = Decrypt(extended);

            if (user.Confirmation == decrypted) {
                // Update the user's password
                var currentUser = await GetUser(user.TenantId, StringValue(user.Email));
                if (currentUser != null && currentUser.ActionResponse.Result) {
                    currentUser.Password = HashPassword(user.Password);

                    var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == currentUser.UserId);
                    if (rec != null) {
                        rec.Password = currentUser.Password;
                        await data.SaveChangesAsync();

                        output = currentUser;
                    } else {
                        output.ActionResponse.Messages.Add("Unable to confirm and update password.");
                    }
                }
            } else {
                output.ActionResponse.Messages.Add("Invalid Confirmation Code");
            }
        }

        return output;
    }

    public async Task<DataObjects.ActiveUser> GetActiveUser(Guid userId)
    {
        var output = new DataObjects.ActiveUser();

        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == userId && x.LastLogin != null && x.LastLogin > DateTime.UtcNow.AddMinutes(-1));
        if(rec != null) {
            var preferences = DeserializeObject<DataObjects.UserPreferences>(rec.Preferences);
            if (preferences == null) {
                preferences = new DataObjects.UserPreferences();
            }

            output = new DataObjects.ActiveUser {
                UserId = rec.UserId,
                TenantId = GuidValue(rec.TenantId),
                Admin = rec.Admin,
                FirstName = rec.FirstName,
                LastAccess = rec.LastLogin,
                LastName = rec.LastName,
                TenantName = "",
                DisplayName = "",
                Photo = await GetUserPhoto(rec.UserId),
                UserPreferences = preferences,
            };
        }

        return output;
    }

    public async Task<List<DataObjects.ActiveUser>> GetActiveUsers(DataObjects.User CurrentUser)
    {
        var output = new List<DataObjects.ActiveUser>();

        List<User>? recs = null;

        if (CurrentUser.AppAdmin) {
            recs = await data.Users.Where(x => x.LastLogin != null && x.LastLogin > DateTime.UtcNow.AddMinutes(-1)).ToListAsync();
        } else if (CurrentUser.Enabled) {
            recs = await data.Users.Where(x => x.TenantId == CurrentUser.TenantId && x.LastLogin != null && x.LastLogin > DateTime.UtcNow.AddMinutes(-1)).ToListAsync();
        }

        if (recs != null && recs.Any()) {
            foreach (var rec in recs) {
                var preferences = DeserializeObject<DataObjects.UserPreferences>(rec.Preferences);
                if (preferences == null) {
                    preferences = new DataObjects.UserPreferences();
                }

                output.Add(new DataObjects.ActiveUser {
                    UserId = rec.UserId,
                    TenantId = GuidValue(rec.TenantId),
                    Admin = rec.Admin,
                    FirstName = rec.FirstName,
                    LastAccess = rec.LastLogin,
                    LastName = rec.LastName,
                    TenantName = "",
                    DisplayName = "",
                    Photo = await GetUserPhoto(rec.UserId),
                    UserPreferences = preferences,
                });
            }
        }

        return output;
    }

    private async Task<List<DataObjects.ActiveUser>> GetActiveUsersForSignalrUpdate(Guid TenantId)
    {
        var output = await GetActiveUsers(new DataObjects.User { TenantId = TenantId, Enabled = true });
        return output;
    }

    public async Task<string> GetDisplayNameFromUserId(Guid? UserId, bool LastNameFirst = false, DataObjects.User? CurrentUser = null)
    {
        string output = String.Empty;
        if (UserId.HasValue && UserId != Guid.Empty) {
            User? rec = null;

            if(AdminUser(CurrentUser)) {
                rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == (Guid)UserId);
            } else {
                rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == (Guid)UserId && x.Deleted != true);
            }
            
            if (rec != null) {
                if (LastNameFirst) {
    
                    output = DisplayNameFromLastAndFirst(rec.LastName, rec.FirstName, rec.Email);
                } else {
                    output = rec.FirstName + " " + rec.LastName;
                }

                if (BooleanValue(rec.Deleted)) {
                    output = "DELETE USER - " + output;
                }
            }
        }
        return output;
    }

    public async Task<string> GetEmailFromUserId(Guid? UserId, DataObjects.User? CurrentUser = null)
    {
        string output = String.Empty;
        if (UserId.HasValue && UserId != Guid.Empty) {
            User? rec = null;

            if(AdminUser(CurrentUser)) {
                rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == (Guid)UserId);
            } else {
                rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == (Guid)UserId && x.Deleted != true);
            }
            
            if (rec != null && !String.IsNullOrEmpty(rec.Email)) {
                output = rec.Email;
            }
        }
        return output;
    }

    private async Task<DataObjects.User> GetExistingUser(Guid TenantId, string Lookup, DataObjects.UserLookupType Type, bool AddIfNotFound = true)
    {
        DataObjects.User output = new DataObjects.User();

        EFModels.EFModels.User? rec = null;

        switch (Type) {
            case DataObjects.UserLookupType.Email:
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Email != null && x.Email.ToLower() == Lookup.ToLower());
                break;

            case DataObjects.UserLookupType.EmployeeId:
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.EmployeeId != null && x.EmployeeId.ToLower() == Lookup.ToLower());
                break;

            case DataObjects.UserLookupType.Guid:
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.UserId.ToString() == Lookup);
                break;

            case DataObjects.UserLookupType.Username:
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Username != null && x.Username.ToLower() == Lookup.ToLower());
                break;
        }

        if (rec != null) {
            if (BooleanValue(rec.Deleted)) {
                output.ActionResponse.Messages.Add("User Has Been Deleted");
            } else {
                output = await GetUser(rec.UserId);
            }
        }

        return output;
    }

    public async Task<string> GetFirstNameFromUserId(Guid? UserId)
    {
        string output = String.Empty;
        if (UserId.HasValue && UserId != Guid.Empty) {
            var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == (Guid)UserId && x.Deleted != true);
            if (rec != null && !String.IsNullOrEmpty(rec.FirstName)) {
                output = rec.FirstName;
            }
        }
        return output;
    }

    public async Task<DataObjects.User> GetUser(Guid TenantId, string UserName)
    {
        DataObjects.User output = new DataObjects.User();

        if (!String.IsNullOrWhiteSpace(UserName)) {
            User? rec = null;

            if (UserName.Contains("@")) {
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Email != null && x.Email.ToLower() == UserName.ToLower());
            } else {
                rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Username.ToLower() == UserName.ToLower());
            }

            if (rec != null) {
                if (BooleanValue(rec.Deleted)) {
                    output.ActionResponse.Messages.Add("User Has Been Deleted");
                } else {
                    output = await GetUser(rec.UserId);
                }
            }

        }

        return output;
    }

    public async Task<DataObjects.User> GetUser(Guid UserId, bool ValidateMainAdminAccess = false, DataObjects.User? CurrentUser = null)
    {
        DataObjects.User output = new DataObjects.User();

        output.AppAdmin = await UserIsMainAdmin(UserId);
        // If a user is a MainAdmin user, they need to have an account in every tenant.
        // That account in the tenant doesn't have to be an Admin user in that tenant, as
        // they will always be considered an Admin in a tenant if they are an admin in the
        // Admin account (Guid1). This needs to be done before getting the rest of the
        // user details, so that when we get to the point of getting all their Tenant
        // and UserTenant objects they will have the full list.
        if (output.AppAdmin && ValidateMainAdminAccess) {
            await ValidateMainAdminUser(UserId);
        }

        User? rec = null;

        if(AdminUser(CurrentUser)) {
            rec = await data.Users
                .FirstOrDefaultAsync(x => x.UserId == UserId);
        } else {
            rec = await data.Users
                .FirstOrDefaultAsync(x => x.UserId == UserId && x.Deleted != true);
        }

        if (rec != null) {
            var preferences = DeserializeObject<DataObjects.UserPreferences>(rec.Preferences);
            if (preferences == null) {
                preferences = new DataObjects.UserPreferences();
            }

            output = new DataObjects.User {
                ActionResponse = GetNewActionResponse(true),
                Added = rec.Added,
                AddedBy = LastModifiedDisplayName(rec.AddedBy),
                TenantId = GuidValue(rec.TenantId),
                Admin = rec.Admin,
                AppAdmin = output.AppAdmin,
                Email = rec.Email,
                Phone = rec.Phone,
                Photo = await GetUserPhoto(UserId),
                Enabled = rec.Enabled,
                Deleted = rec.Deleted,
                DeletedAt = rec.DeletedAt,
                FirstName = rec.FirstName,
                LastLogin = rec.LastLogin.HasValue ? Convert.ToDateTime(rec.LastLogin) : (DateTime?)null,
                LastLoginSource = rec.LastLoginSource,
                LastModified = rec.LastModified,
                LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                LastName = rec.LastName,
                ManageFiles = rec.ManageFiles,
                Title = rec.Title,
                UserId = rec.UserId,
                Username = rec.Username,
                EmployeeId = rec.EmployeeId,
                Password = String.Empty,
                PreventPasswordChange = rec.PreventPasswordChange,
                HasLocalPassword = !String.IsNullOrWhiteSpace(rec.Password),
                LastLockoutDate = rec.LastLockoutDate,
                Source = rec.Source,
                UserPreferences = preferences,
            };

            // If the user is in multiple tenants, add all of the userid/tenantid pairs to the UserAccounts property.
            output.UserAccounts = await GetUserAccounts(output.Email);

            if (output.AppAdmin) {
                output.Admin = true;
            }

            if (output.Admin) {
                output.ManageFiles = true;
            }


            output.DisplayName = DisplayNameFromLastAndFirst(output.LastName, output.FirstName, output.Email);

        }

        return output;
    }

    public async Task<List<DataObjects.UserAccount>> GetUserAccounts(string? email)
    {
        var output = new List<DataObjects.UserAccount>();

        if (!String.IsNullOrWhiteSpace(email)) {
            var recs = await data.Users
                .Where(x => x.Email.ToLower() == email.ToLower())
                .Select(x => new { x.UserId, x.TenantId, x.Enabled, x.FirstName, x.LastName })
                .ToListAsync();

            if (recs != null && recs.Any()) {
                foreach (var rec in recs) {
                    output.Add(new DataObjects.UserAccount {
                        UserId = rec.UserId,
                        TenantId = rec.TenantId,
                        Enabled = rec.Enabled,
                        FirstName = StringValue(rec.FirstName),
                        LastName = StringValue(rec.LastName),
                    });
                }
            }
        }

        return output;

    }

    public async Task<DataObjects.User> GetUserByEmailAddress(Guid TenantId, string EmailAddress, bool AddIfNotFound = true)
    {
        var output = await GetExistingUser(TenantId, EmailAddress, DataObjects.UserLookupType.Email, AddIfNotFound);
        return output;
    }

    public async Task<DataObjects.User> GetUserByEmployeeId(Guid TenantId, string EmployeeId, bool AddIfNotFound = false)
    {
        var output = await GetExistingUser(TenantId, EmployeeId, DataObjects.UserLookupType.EmployeeId, AddIfNotFound);
        return output;

    }

    public async Task<DataObjects.User> GetUserByUsername(Guid TenantId, string Username, bool AddIfNotFound = false)
    {
        var output = await GetExistingUser(TenantId, Username, DataObjects.UserLookupType.Username, AddIfNotFound);
        return output;

    }

    public async Task<DataObjects.User> GetUserByUsernameOrEmail(Guid TenantId, string search, bool AddIfNotFound = true)
    {
        DataObjects.User output = new DataObjects.User();
        if (!String.IsNullOrWhiteSpace(search)) {
            if (search.Contains("@")) {
                output = await GetUserByEmailAddress(TenantId, search, AddIfNotFound);
            } else {
                output = await GetUserByUsername(TenantId, search, AddIfNotFound);
            }
        }
        return output;
    }

    public async Task<string> GetUserDisplayName(Guid? UserId)
    {
        string output = String.Empty;

        if (UserId != null) {
            var rec = await data.Users
                .Select(x => new { x.UserId, x.FirstName, x.LastName, x.Deleted })
                .FirstOrDefaultAsync(x => x.UserId == UserId && x.Deleted != true);
                    
            if(rec != null) {
                output = rec.FirstName + " " + rec.LastName;
            }
        }

        return output;
    }

    public async Task<DataObjects.FilterUsers> GetUsersFiltered(DataObjects.FilterUsers filter, DataObjects.User? CurrentUser = null)
    {
        DataObjects.FilterUsers output = filter;
        output.ActionResponse = GetNewActionResponse();
        output.Records = null;

        var language = GetTenantLanguage(output.TenantId, StringValue(output.CultureCode));

        output.Columns = new List<DataObjects.FilterColumn> {
            new DataObjects.FilterColumn{
                Align = "center",
                Label = "",
                TipText = "",
                Sortable = false,
                DataElementName = "photo",
                DataType = "photo"
            },
            new DataObjects.FilterColumn{
                Align = "",
                Label = GetLanguageItem("FirstName", language),
                TipText = "",
                Sortable = true,
                DataElementName = "firstName",
                DataType = "string"
            },
            new DataObjects.FilterColumn{
                Align = "",
                Label = GetLanguageItem("LastName", language),
                TipText = "",
                Sortable = true,
                DataElementName = "lastName",
                DataType = "string"
            },
            new DataObjects.FilterColumn{
                Align = "",
                Label = GetLanguageItem("Email", language),
                TipText = "",
                Sortable = true,
                DataElementName = "email",
                DataType = "email"
            },
            new DataObjects.FilterColumn{
                Align = "",
                Label = GetLanguageItem("Username", language),
                TipText = "",
                Sortable = true,
                DataElementName = "username",
                DataType = "string"
            }
        };

        var settings = GetTenantSettings(output.TenantId);
        List<string> blockedModules = settings.ModuleHideElements;
        List<string> optIn = settings.ModuleOptInElements;

        bool showEmployeeId = ModuleEnabled("employeeid", settings);

        if (showEmployeeId) {
            output.Columns.Add(new DataObjects.FilterColumn {
                Align = "",
                Label = GetLanguageItem("EmployeeId", language),
                TipText = "",
                Sortable = true,
                DataElementName = "employeeId",
                DataType = "string"
            });
        }


        output.Columns.Add(new DataObjects.FilterColumn {
            Align = "center",
            Label = "icon:RecordsTableIconEnabled",
            TipText = "Enabled",
            Sortable = true,
            DataElementName = "enabled",
            DataType = "boolean"
        });

        output.Columns.Add(new DataObjects.FilterColumn {
            Align = "center",
            Label = "icon:RecordsTableIconAdmin",
            TipText = "Admin",
            Sortable = true,
            DataElementName = "admin",
            DataType = "boolean"
        });

        output.Columns.Add(new DataObjects.FilterColumn {
            Align = "",
            Label = GetLanguageItem("LastLogin", language),
            TipText = "",
            Sortable = true,
            DataElementName = "lastLogin",
            DataType = "datetime"
        });

        output.Columns.Add(new DataObjects.FilterColumn { 
            Align = "center",
            Label = "#",
            TipText = GetLanguageItem("FailedLoginAttempts", language),
            Sortable = false,
            DataElementName = "failedLoginAttempts",
            DataType = "number",
        });


        IQueryable<User>? recs = null;

        if(AdminUser(CurrentUser)) {
            recs = data.Users
                .Where(x => x.TenantId == output.TenantId && x.Username != null && x.Username.ToLower() != "admin");

            if (!filter.IncludeDeletedItems) {
                recs = recs.Where(x => x.Deleted != true);
            }
        } else {
            recs = data.Users
                .Where(x => x.TenantId == output.TenantId && x.Deleted != true && x.Username != null && x.Username.ToLower() != "admin");
        }

        if (!String.IsNullOrWhiteSpace(output.Enabled)) {
            switch (output.Enabled.ToUpper()) {
                case "ENABLED":
                    recs = recs.Where(x => x.Enabled == true);
                    break;

                case "DISABLED":
                    recs = recs.Where(x => x.Enabled != true);
                    break;
            }
        }

        if (!String.IsNullOrWhiteSpace(output.Admin)) {
            switch (output.Admin.ToUpper()) {
                case "ADMIN":
                    recs = recs.Where(x => x.Admin == true);
                    break;

                case "STANDARD":
                    recs = recs.Where(x => x.Admin != true);
                    break;
            }
        }

        // Add any filters
        if (!String.IsNullOrEmpty(output.Keyword)) {
            string keyword = output.Keyword.ToLower();
            
            recs = recs.Where(x => (x.LastName != null && x.LastName.ToLower().Contains(keyword))
                || (x.FirstName != null && x.FirstName.ToLower().Contains(keyword))
                || (x.Email != null && x.Email.ToLower().Contains(keyword))
                || (x.Username != null && x.Username.ToLower().Contains(keyword))
            );
        }

        if (String.IsNullOrWhiteSpace(output.Sort)) {
            output.Sort = "lastLogin";
            output.SortOrder = "DESC";
        }

        if (String.IsNullOrWhiteSpace(output.SortOrder)) {
            switch (output.Sort.ToUpper()) {
                case "LASTLOGIN":
                    output.SortOrder = "DESC";
                    break;

                default:
                    output.SortOrder = "ASC";
                    break;
            }
        }

        bool Ascending = true;
        if (StringValue(output.SortOrder).ToUpper() == "DESC") {
            Ascending = false;
        }

        switch (StringValue(output.Sort).ToUpper()) {
            case "FIRSTNAME":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.FirstName).ThenBy(x => x.LastName);
                } else {
                    recs = recs.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName);
                }
                break;

            case "LASTNAME":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.LastName).ThenBy(x => x.FirstName);
                } else {
                    recs = recs.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName);
                }
                break;

            case "EMAIL":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.Email);
                } else {
                    recs = recs.OrderByDescending(x => x.Email);
                }
                break;

            case "USERNAME":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.Username);
                } else {
                    recs = recs.OrderByDescending(x => x.Username);
                }
                break;

            case "EMPLOYEEID":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.EmployeeId).ThenBy(x => x.LastName).ThenBy(x => x.FirstName);
                } else {
                    recs = recs.OrderByDescending(x => x.EmployeeId).ThenByDescending(x => x.LastName).ThenByDescending(x => x.FirstName);
                }
                break;

            case "ENABLED":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.Enabled == false).ThenBy(x => x.LastName).ThenBy(x => x.FirstName);
                } else {
                    recs = recs.OrderBy(x => x.Enabled == true).ThenByDescending(x => x.LastName).ThenByDescending(x => x.FirstName);
                }
                break;

            case "ADMIN":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.Admin == false).ThenBy(x => x.LastName).ThenBy(x => x.FirstName);
                } else {
                    recs = recs.OrderBy(x => x.Admin == true).ThenByDescending(x => x.LastName).ThenByDescending(x => x.FirstName);
                }
                break;

            case "LASTLOGIN":
                if (Ascending) {
                    recs = recs.OrderBy(x => x.LastLogin).ThenBy(x => x.LastName).ThenBy(x => x.FirstName);
                } else {
                    recs = recs.OrderByDescending(x => x.LastLogin).ThenByDescending(x => x.LastName).ThenByDescending(x => x.FirstName);
                }
                break;
        }

        if (recs != null && recs.Count() > 0) {

            int TotalRecords = recs.Count();
            output.RecordCount = TotalRecords;

            if (output.RecordsPerPage > 0) {
                // We are filtering records per page
                if (output.RecordsPerPage >= TotalRecords) {
                    output.Page = 1;
                    output.PageCount = 1;
                } else {
                    // Figure out the page count
                    if (output.Page < 1) { output.Page = 1; }
                    if (output.RecordsPerPage < 1) { output.RecordsPerPage = 25; }
                    decimal decPages = (decimal)TotalRecords / (decimal)output.RecordsPerPage;
                    decPages = Math.Ceiling(decPages);
                    output.PageCount = (int)decPages;

                    if (output.Page > output.PageCount) {
                        output.Page = output.PageCount;
                    }

                    if (output.Page > 1) {
                        recs = recs.Skip((output.Page - 1) * output.RecordsPerPage).Take(output.RecordsPerPage);
                    } else {
                        recs = recs.Take(output.RecordsPerPage);
                    }

                }
            }

            List<DataObjects.User> records = new List<DataObjects.User>();

            foreach (var rec in recs) {
                var u = new DataObjects.User {
                    ActionResponse = GetNewActionResponse(true),
                    Added = rec.Added,
                    //AddedBy = LastModifiedDisplayName(rec.AddedBy),
                    Admin = rec.Admin,
                    Email = rec.Email,
                    EmployeeId = rec.EmployeeId,
                    Enabled = rec.Enabled,
                    Deleted = rec.Deleted,
                    DeletedAt = rec.DeletedAt,
                    FirstName = rec.FirstName,
                    LastLogin = rec.LastLogin,
                    LastLoginSource = rec.LastLoginSource,
                    LastName = rec.LastName,
                    ManageFiles = rec.ManageFiles,
                    Phone = rec.Phone,
                    UserId = rec.UserId,
                    Username = rec.Username,
                    Photo = await GetUserPhoto(rec.UserId),
                    PreventPasswordChange = rec.PreventPasswordChange,
                    HasLocalPassword = !String.IsNullOrWhiteSpace(rec.Password),
                    LastLockoutDate = rec.LastLockoutDate,
                    LastModified = rec.LastModified,
                    LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                    Source = rec.Source,
                    FailedLoginAttempts = IntValue(rec.FailedLoginAttempts),
                };

                records.Add(u);
            }

            output.Records = records;
        }

        output.ActionResponse.Result = true;

        return output;
    }

    public async Task<DataObjects.User> GetUserFromToken(string? Token, string fingerprint = "")
    {
        DataObjects.User output = new DataObjects.User();

        if (!String.IsNullOrWhiteSpace(Token)) {
            // Need to try each active Tenant to see which key can decrypt this token.
            // Since all RSA keys are unique, the first that decrypts the token and finds a valid user is the valid tenant.
            var tenants = await GetTenants();
            if (tenants.Any()) { 
                foreach(var tenant in tenants.Where(x => x.Enabled == true)) {
                    Guid UserId = Guid.Empty;
                    string tokenFingerprint = String.Empty;

                    Dictionary<string, object> decrypted = JwtDecode(tenant.TenantId, Token);
                    if(decrypted != null && decrypted.Any()) {
                        try {
                            string guid = decrypted["UserId"] + String.Empty;
                            UserId = new Guid(guid);
                        } catch { }

                        try {
                            tokenFingerprint += decrypted["Fingerprint"];
                        } catch { }

                        if(UserId != Guid.Empty) {
                            if (!String.IsNullOrWhiteSpace(fingerprint) || !String.IsNullOrWhiteSpace(tokenFingerprint)) {
                                // Make sure the fingerprint matches
                                if (fingerprint != tokenFingerprint) {
                                    return output;
                                }
                            }

                            output = await GetUser(UserId);
                            return output;
                        }
                    }
                }
            }
        }

        return output;
    }

    public async Task<DataObjects.User> GetUserFromToken(Guid TenantId, string? Token, string fingerprint = "")
    {
        DataObjects.User output = new DataObjects.User();

        if (!String.IsNullOrWhiteSpace(Token)) {
            var cachedUser = CacheStore.GetCachedItem<DataObjects.User>(TenantId, "user-from-token-" + Token);
            if (cachedUser != null && cachedUser.TenantId == TenantId && cachedUser.ActionResponse.Result) {
                return cachedUser;
            }

            Guid UserId = Guid.Empty;
            string tokenFingerprint = String.Empty;

            Dictionary<string, object> decrypted = JwtDecode(TenantId, Token);
            try {
                string guid = decrypted["UserId"] + String.Empty;
                UserId = new Guid(guid);
            } catch { }

            try {
                tokenFingerprint += decrypted["Fingerprint"];
            } catch { }

            if (UserId != Guid.Empty) {
                if (!String.IsNullOrWhiteSpace(fingerprint) || !String.IsNullOrWhiteSpace(tokenFingerprint)) {
                    // Make sure the fingerprint matches
                    if (fingerprint != tokenFingerprint) {
                        return output;
                    }
                }

                output = await GetUser(UserId);

                // Cache this response for a while so that multiple calls to this by the API call to get the current user
                // and the CustomAuthenticationHandler will not result in multiple lookups in the database.
                CacheStore.SetCacheItem(TenantId, "user-from-token-" + Token, output, DateTimeOffset.Now.AddMinutes(5));
            }
        }

        return output;
    }

    public async Task<List<DataObjects.UserListing>> GetUserListing(Guid TenantId)
    {
        List<DataObjects.UserListing> output = new List<DataObjects.UserListing>();

        var recs = await data.Users
            .Where(x => x.TenantId == TenantId && x.Username.ToLower() != "admin" && x.Deleted != true)
            .Select(x => new { x.UserId, x.FirstName, x.LastName, x.Email, x.Username, x.Enabled, x.Admin, x.Deleted, x.DeletedAt })
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync();

        if (recs != null && recs.Any()) {
            foreach (var rec in recs) {
                var admin = BooleanValue(rec.Admin);
                var appAdmin = await UserIsMainAdmin(rec.UserId);

                output.Add(new DataObjects.UserListing {
                    UserId = rec.UserId,
                    FirstName = rec.FirstName,
                    LastName = rec.LastName,
                    Email = rec.Email,
                    Username = rec.Username,
                    Enabled = BooleanValue(rec.Enabled),
                    Admin = admin || appAdmin,
                    Deleted = BooleanValue(rec.Deleted),
                    DeletedAt = rec.DeletedAt,
                });
            }
        }

        return output;
    }

    public async Task<Guid?> GetUserPhoto(Guid UserId)
    {
        Guid? output = (Guid?)null;
        try {
            var rec = await data.FileStorages.FirstOrDefaultAsync(x => x.ItemId == null && x.UserId == UserId && x.Deleted != true);
            if (rec != null) {
                output = rec.FileId;
            }
        } catch (Exception ex) {
            if (ex != null) {

            }
        }
        return output;
    }

    public async Task<DataObjects.SimpleResponse> GetUserPhotoId(Guid UserId)
    {
        DataObjects.SimpleResponse output = new DataObjects.SimpleResponse();

        var rec = await data.FileStorages.FirstOrDefaultAsync(x => x.ItemId == null && x.UserId == UserId && x.Deleted != true);
        if(rec != null) {
            output = new DataObjects.SimpleResponse { 
                Result = true,
                Message = rec.FileId.ToString(),
            };
        }

        return output;
    }

    public async Task<List<DataObjects.User>> GetUsers(Guid TenantId)
    {
        List<DataObjects.User> output = new List<DataObjects.User>();

        var recs = await data.Users.Where(x => x.TenantId == TenantId && x.Deleted != true)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync();
        if (recs != null && recs.Count() > 0) {
            foreach (var rec in recs) {
                var u = new DataObjects.User {
                    ActionResponse = GetNewActionResponse(true),
                    Added = rec.Added,
                    AddedBy = LastModifiedDisplayName(rec.AddedBy),
                    TenantId = TenantId,
                    Admin = rec.Admin,
                    Email = rec.Email,
                    Phone = rec.Phone,
                    Enabled = rec.Enabled,
                    Deleted = rec.Deleted,
                    DeletedAt = rec.DeletedAt,
                    FirstName = rec.FirstName,
                    LastLogin = rec.LastLogin,
                    LastLoginSource = rec.LastLoginSource,
                    LastName = rec.LastName,
                    ManageFiles = rec.ManageFiles,
                    Photo = await GetUserPhoto(rec.UserId),
                    UserId = rec.UserId,
                    Username = rec.Username,
                    EmployeeId = rec.EmployeeId,
                    Password = String.Empty,
                    PreventPasswordChange = rec.PreventPasswordChange,
                    HasLocalPassword = !String.IsNullOrWhiteSpace(rec.Password),
                    LastLockoutDate = rec.LastLockoutDate,
                    LastModified = rec.LastModified,
                    LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                    Source = rec.Source,
                };

                u.DisplayName = DisplayNameFromLastAndFirst(u.LastName, u.FirstName, u.Email);

                output.Add(u);
            }
        }

        return output;
    }

    public async Task<List<DataObjects.User>> GetUsersForEmailAddress(string? EmailAddress, string fingerprint = "")
    {
        List<DataObjects.User> output = new List<DataObjects.User>();

        if (!String.IsNullOrWhiteSpace(EmailAddress)) {
            var userIds = await data.Users.Where(x => x.Enabled == true && x.Deleted != true && x.Email != null && x.Email.ToLower() == EmailAddress.ToLower())
                .Select(x => x.UserId).ToListAsync();

            if (userIds != null && userIds.Any()) {
                foreach (var userId in userIds) {
                    var user = await GetUser(userId);
                    if (user != null && user.ActionResponse.Result) {
                        user.AuthToken = GetUserToken(user.TenantId, user.UserId, fingerprint);
                        output.Add(user);
                    }
                }
            }
        }

        return output;
    }

    public async Task<List<DataObjects.UserTenant>> GetUserTenantList(string? username, string? email, bool enabledUsersOnly = true)
    {
        List<DataObjects.UserTenant> output = new List<DataObjects.UserTenant>();

        if (!String.IsNullOrEmpty(username) && !String.IsNullOrEmpty(email)) {
            var u1 = await data.Users.Where(x => x.Deleted != true && (x.Username == username || (x.Email != null && x.Email.ToLower() == email.ToLower()))).ToListAsync();
            if (u1 != null && u1.Any()) {
                foreach (var rec in u1) {
                    bool enabled = rec.Enabled;
                    if (enabled || !enabledUsersOnly) {
                        output.Add(new DataObjects.UserTenant { UserId = rec.UserId, TenantId = GuidValue(rec.TenantId) });
                    }
                }
            }
        } else if (!String.IsNullOrEmpty(username)) {
            var u2 = await data.Users.Where(x => x.Deleted != true && x.Username != null && x.Username.ToLower() == username.ToLower()).ToListAsync();
            if (u2 != null && u2.Any()) {
                foreach (var rec in u2) {
                    bool enabled = rec.Enabled;
                    if (enabled || !enabledUsersOnly) {
                        output.Add(new DataObjects.UserTenant { UserId = rec.UserId, TenantId = GuidValue(rec.TenantId) });
                    }
                }
            }
        } else if (!String.IsNullOrEmpty(email)) {
            var u3 = await data.Users.Where(x => x.Deleted != true && x.Email != null && x.Email.ToLower() == email.ToLower()).ToListAsync();
            if (u3 != null && u3.Any()) {
                foreach (var rec in u3) {
                    bool enabled = rec.Enabled;
                    if (enabled || !enabledUsersOnly) {
                        output.Add(new DataObjects.UserTenant { UserId = rec.UserId, TenantId = GuidValue(rec.TenantId) });
                    }
                }
            }
        }

        return output;
    }

    public async Task<List<DataObjects.Tenant>?> GetUserTenants(string? username, string? email, bool enabledUsersOnly = true)
    {
        List<DataObjects.Tenant>? output = null;

        List<Guid> tenantIds = new List<Guid>();

        if (!String.IsNullOrEmpty(username) && !String.IsNullOrEmpty(email)) {
            var u1 = await data.Users.Where(x => x.Deleted != true && (x.Username != null && x.Username.ToLower() == username.ToLower()) ||
                (x.Email != null && x.Email.ToLower() == email.ToLower())).ToListAsync();
            if (u1 != null && u1.Any()) {
                if (enabledUsersOnly) {
                    tenantIds = u1.Where(x => x.Enabled == true).Select(x => x.TenantId).Distinct().ToList();
                } else {
                    tenantIds = u1.Select(x => x.TenantId).Distinct().ToList();
                }
            }
        } else if (!String.IsNullOrEmpty(username)) {
            var u2 = await data.Users.Where(x => x.Deleted != true && x.Username != null && x.Username.ToLower() == username.ToLower()).ToListAsync();
            if (u2 != null && u2.Any()) {
                if (enabledUsersOnly) {
                    tenantIds = u2.Where(x => x.Enabled == true).Select(x => x.TenantId).Distinct().ToList();
                } else {
                    tenantIds = u2.Select(x => x.TenantId).Distinct().ToList();
                }
            }
        } else if (!String.IsNullOrEmpty(email)) {
            var u3 = await data.Users.Where(x => x.Deleted != true && x.Email != null && x.Email.ToLower() == email.ToLower()).ToListAsync();
            if (u3 != null && u3.Any()) {
                if (enabledUsersOnly) {
                    tenantIds = u3.Where(x => x.Enabled == true).Select(x => x.TenantId).Distinct().ToList();
                } else {
                    tenantIds = u3.Select(x => x.TenantId).Distinct().ToList();
                }
            }
        }

        if (tenantIds != null && tenantIds.Any()) {
            foreach (var tenantId in tenantIds) {
                var tenant = GetTenant((Guid)tenantId);
                if (tenant != null) {
                    if (output == null) {
                        output = new List<DataObjects.Tenant>();
                    }
                    output.Add(tenant);
                }
            }
        }

        return output;
    }

    public string GetUserToken(Guid TenantId, Guid UserId, string fingerprint = "")
    {
        // jwtencode
        Dictionary<string, object> Payload = new Dictionary<string, object> {
            { "UserId", UserId }
        };

        if (!String.IsNullOrWhiteSpace(fingerprint)) {
            Payload.Add("Fingerprint", fingerprint);
        }

        string output = JwtEncode(TenantId, Payload);
        return output;
    }

    public string LastModifiedDisplayName(string? lastModifiedBy)
    {
        string output = StringValue(lastModifiedBy);

        if (!String.IsNullOrWhiteSpace(lastModifiedBy) && lastModifiedBy.Length == 36) {
            // See if this is a Guid
            Guid guidTest = Guid.Empty;
            try {
                guidTest = new Guid(lastModifiedBy);

                var rec = data.Users
                    .Select(x => new { x.UserId, x.FirstName, x.LastName })
                    .FirstOrDefault(x => x.UserId == guidTest);

                if(rec != null) {
                    if(!String.IsNullOrWhiteSpace(rec.FirstName) || !String.IsNullOrWhiteSpace(rec.LastName)) {
                        output = rec.FirstName + " " + rec.LastName;
                    }
                }
            } catch { }
        }

        return output;
    }

    public string LastModifiedDisplayName(DataObjects.User? CurrentUser)
    {
        if(CurrentUser != null) {
            return LastModifiedDisplayName(CurrentUser.UserId.ToString());
        } else {
            return String.Empty;
        }
    }

    public async Task<DataObjects.BooleanResponse> ResetUserPassword(DataObjects.UserPasswordReset reset, DataObjects.User currentUser)
    {
        DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();
        if (reset == null || reset.UserId == Guid.Empty) {
            output.Messages.Add("No UserId Specified");
            return output;
        }

        bool validatePassword = true;
        if(currentUser.Admin && currentUser.UserId != reset.UserId) {
            validatePassword = false;
        }

        if (validatePassword) {
            if(String.IsNullOrWhiteSpace(reset.CurrentPassword) || String.IsNullOrWhiteSpace(reset.NewPassword)) {
                output.Messages.Add("Missing Current or New Password");
            }
        }else if (String.IsNullOrWhiteSpace(reset.NewPassword)) {
            output.Messages.Add("Missing New Password");
        }
        if (output.Messages.Any()) {
            return output;
        }

        bool adminPasswordReset = false;
        string emailAddress = String.Empty;

        var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == currentUser.TenantId && x.UserId == reset.UserId);
        if (rec == null) {
            output.Messages.Add("UserId Not Found");
        } else {
            emailAddress = rec.Email;
            adminPasswordReset = rec.Username.ToLower() == "admin";

            if (validatePassword) {
                // See if the password is in the hash format and can be validated.
                if (!HashPasswordValidate(reset.CurrentPassword, rec.Password)) {
                    output.Messages.Add("Incorrect Current Password");
                    return output;
                }
            }

            rec.Password = HashPassword(reset.NewPassword);
            try {
                await data.SaveChangesAsync();
                output.Result = true;
                output.Messages.Add("Password Reset");

                // If this is the admin user, reset all other admin passwords to match.
                if (adminPasswordReset) {
                    var recs = await data.Users.Where(x => x.TenantId != currentUser.TenantId && x.Username.ToLower() == "admin").ToListAsync();
                    if (recs != null && recs.Any()) {
                        foreach (var additionalRecord in recs) {
                            additionalRecord.Password = HashPassword(reset.NewPassword);
                            await data.SaveChangesAsync();
                        }
                    }
                }

                // If this is an AllAccounts reset, update all other passwords for this user.
                if (reset.AllAccounts) {
                    var recs = await data.Users.Where(x => x.TenantId != reset.TenantId && x.Email.ToLower() == emailAddress.ToLower()).ToListAsync();
                    if (recs != null && recs.Any()) {
                        foreach (var additionalRecord in recs) {
                            additionalRecord.Password = HashPassword(reset.NewPassword);
                            await data.SaveChangesAsync();
                        }
                    }
                }
            } catch (Exception ex) {
                output.Messages.Add("Error Resetting Password:");
                output.Messages.AddRange(RecurseException(ex));
            }
        }

        return output;
    }

    public async Task<DataObjects.User> SaveUser(DataObjects.User user, DataObjects.User? CurrentUser = null)
    {
        var output = user;
        output.ActionResponse = GetNewActionResponse();

        string originalEmail = "";
        string originalUsername = "";

        DateTime now = DateTime.UtcNow;

        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == output.UserId);
        bool newRecord = false;
        if (rec == null) {
            if (output.UserId == Guid.Empty) {
                rec = new EFModels.EFModels.User();
                rec.Added = now;
                rec.AddedBy = CurrentUserIdString(CurrentUser);
                output.UserId = Guid.NewGuid();
                rec.UserId = output.UserId;
                rec.Deleted = false;
                newRecord = true;
            } else {
                output.ActionResponse.Messages.Add("Error Saving User " + output.UserId.ToString() + " - Record No Longer Exists");
                return output;
            }
        } else {
            originalEmail = StringValue(rec.Email);
            originalUsername = StringValue(rec.Username);
        }

        if(String.IsNullOrWhiteSpace(output.Username) && !String.IsNullOrWhiteSpace(output.Email)) {
            output.Username = output.Email;
        }

        // If this is a new record make sure the username or email are not already in use.
        if (newRecord || StringValue(output.Email).ToLower() != originalEmail.ToLower()) {
            var emailInUse = await data.Users.FirstOrDefaultAsync(x => x.TenantId == output.TenantId && x.Email != null && x.Email.ToLower() == StringValue(output.Email).ToLower());
            if(emailInUse != null) {
                output.ActionResponse.Messages.Add("Email '" + output.Email + "' Already in use.");
                return output;
            }
        }

        if (newRecord || StringValue(output.Username).ToLower() != originalUsername.ToLower()) {
            var usernameInUse = await data.Users.FirstOrDefaultAsync(x => x.TenantId == output.TenantId && x.Username.ToLower() == StringValue(output.Username).ToLower());
            if (usernameInUse != null) {
                output.ActionResponse.Messages.Add("Username '" + output.Username + "' Already in use.");
                return output;
            }
        }

        rec.TenantId = output.TenantId;

        output.Email = MaxStringLength(output.Email, 100);
        rec.Email = output.Email;

        output.EmployeeId = MaxStringLength(output.EmployeeId, 50);
        rec.EmployeeId = output.EmployeeId;

        output.FirstName = MaxStringLength(output.FirstName, 100);
        rec.FirstName = output.FirstName;

        rec.LastLogin = output.LastLogin.HasValue ? (DateTime)output.LastLogin : (DateTime?)null;

        output.LastLoginSource = MaxStringLength(output.LastLoginSource, 50);
        if (!String.IsNullOrWhiteSpace(output.LastLoginSource)) {
            rec.LastLoginSource = output.LastLoginSource;
        }

        output.LastName = MaxStringLength(output.LastName, 100);
        rec.LastName = output.LastName;

        output.Phone = MaxStringLength(output.Phone, 20);
        rec.Phone = output.Phone;

        output.Title = MaxStringLength(output.Title, 255);
        rec.Title = output.Title;

        output.Source = MaxStringLength(output.Source, 100);
        rec.Source = output.Source;

        output.Username = MaxStringLength(output.Username, 100);
        rec.Username = output.Username;

        rec.Enabled = output.Enabled;
        rec.Admin = output.Admin;
        rec.ManageFiles = output.ManageFiles;
        rec.PreventPasswordChange = output.PreventPasswordChange;

        rec.LastModified = now;

        if(CurrentUser != null) {
            rec.LastModifiedBy = CurrentUserIdString(CurrentUser);

            if (CurrentUser.Admin) {
                rec.Deleted = output.Deleted;
            }
        }

        try {
            if (newRecord) {
                data.Users.Add(rec);
            }
            await data.SaveChangesAsync();
            output.ActionResponse.Result = true;

            // Clear any cached items.
            CacheStore.ClearAllUserItems();

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = output.TenantId,
                ItemId = output.UserId,
                UpdateType = DataObjects.SignalRUpdateType.User,
                Message = "Saved",
                UserId = CurrentUserId(CurrentUser),
                Object = output,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Saving User " + output.UserId.ToString() + ":");
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.User> SaveUserByUsername(DataObjects.User user, bool CreateIfNotFound, DataObjects.User? CurrentUser = null)
    {
        user.ActionResponse = GetNewActionResponse();

        if (GuidValue(user.TenantId) == Guid.Empty) {
            user.ActionResponse.Messages.Add("Invalid TenantId");
            return user;
        }

        DateTime now = DateTime.UtcNow;

        bool newRecord = false;

        string username = StringValue(user.Username);

        var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == user.TenantId && x.Deleted != true && x.Username != null && x.Username.ToLower() == username.ToLower());
        if (rec == null) {
            if (CreateIfNotFound) {
                newRecord = true;
                rec = new EFModels.EFModels.User();
                rec.Added = now;
                rec.AddedBy = CurrentUserIdString(CurrentUser);
                if (user.UserId == Guid.Empty) {
                    // Only create a new UserId if we don't have one, otherwise, use the one supplied
                    user.UserId = Guid.NewGuid();
                }
                rec.TenantId = user.TenantId;
                rec.UserId = user.UserId;
                rec.Deleted = false;
            } else {
                user.ActionResponse.Messages.Add("Error Saving User " + user.UserId.ToString() + " - Record No Longer Exists");
                return user;
            }
        }

        user.Email = MaxStringLength(user.Email, 100);
        rec.Email = user.Email;

        user.EmployeeId = MaxStringLength(user.EmployeeId, 50);
        rec.EmployeeId = user.EmployeeId;

        user.FirstName = MaxStringLength(user.FirstName, 100);
        rec.FirstName = user.FirstName;

        rec.LastLogin = user.LastLogin.HasValue ? (DateTime)user.LastLogin : (DateTime?)null;

        if (!String.IsNullOrWhiteSpace(user.LastLoginSource)) {
            rec.LastLoginSource = user.LastLoginSource;
        }

        user.LastName = MaxStringLength(user.LastName, 100);
        rec.LastName = user.LastName;

        user.Phone = MaxStringLength(user.Phone, 20);
        rec.Phone = user.Phone;

        user.Title = MaxStringLength(user.Title, 255);
        rec.Title = user.Title;

        user.Source = MaxStringLength(user.Source, 100);
        rec.Source = user.Source;

        user.Username = MaxStringLength(user.Username, 100);
        rec.Username = user.Username;

        rec.Enabled = user.Enabled;
        rec.Admin = user.Admin;
        rec.ManageFiles = user.ManageFiles;
        rec.PreventPasswordChange = user.PreventPasswordChange;

        rec.LastModified = now;

        if(CurrentUser != null) {
            rec.LastModifiedBy = CurrentUserIdString(CurrentUser);
        }

        try {
            if (newRecord) {
                data.Users.Add(rec);
            }
            await data.SaveChangesAsync();
            user.ActionResponse.Result = true;

            // Clear any cached items.
            CacheStore.ClearAllUserItems();

            await SignalRUpdate(new DataObjects.SignalRUpdate { 
                TenantId = user.TenantId,
                ItemId = user.UserId,
                UpdateType = DataObjects.SignalRUpdateType.User,
                Message = "Saved",
                UserId = CurrentUserId(CurrentUser),
                Object = user,
            });
        } catch (Exception ex) {
            user.ActionResponse.Messages.Add("Error Saving User " + user.UserId.ToString() + ":");
            user.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return user;
    }

    public async Task<DataObjects.BooleanResponse> SaveUserPreferences(Guid UserId, DataObjects.UserPreferences userPreferences)
    {
        var output = new DataObjects.BooleanResponse();

        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId);
        if (rec != null) {
            try {
                var tenantId = rec.TenantId;

                var previousPreferences = DeserializeObject<DataObjects.UserPreferences>(rec.Preferences);
                if(previousPreferences == null) {
                    previousPreferences = new DataObjects.UserPreferences();
                }

                rec.Preferences = SerializeObject(userPreferences);
                await data.SaveChangesAsync();

                // Any time a user preference is saved push out the update to all clients via SignalR.
                if(previousPreferences.LastNavigationId != userPreferences.LastNavigationId ||
                    previousPreferences.LastView != userPreferences.LastView ||
                    previousPreferences.LastUrl != userPreferences.LastUrl) {

                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        TenantId = tenantId,
                        UpdateType = DataObjects.SignalRUpdateType.UserPreferences,
                        ItemId = Guid.Empty,
                        Message = "Saved",
                        UserId = UserId,
                        Object = userPreferences,
                    });
                }
            } catch (Exception ex) {
                output.Messages.Add("Error Saving User Preferences for User " + UserId.ToString() + ":");
                output.Messages.AddRange(RecurseException(ex));
            }
        } else {
            output.Messages.Add("User '" + UserId.ToString() + "' Not Found");
        }

        return output;
    }

    public async Task<List<DataObjects.User>> SaveUsers(List<DataObjects.User> users, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.User> output = new List<DataObjects.User>();
        foreach (var user in users) {
            var saved = await SaveUser(user, CurrentUser);
            output.Add(saved);
        }
        return output;
    }

    public async Task<DataObjects.User> UnlockUserAccount(Guid UserId, DataObjects.User? CurrentUser = null)
    {
        DataObjects.User output = await GetUser(UserId, false, CurrentUser);

        if (output.ActionResponse.Result) {
            if (output.LastLockoutDate.HasValue) {
                var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId);
                if (rec != null) {
                    rec.LastLockoutDate = null;
                    rec.FailedLoginAttempts = null;
                    await data.SaveChangesAsync();
                    output = await GetUser(UserId, false, CurrentUser);
                } else {
                    output.ActionResponse = GetNewActionResponse(false, "UserId not found.");
                }
            } else {
                output.ActionResponse = GetNewActionResponse(false, "Account Has Already Been Unlocked");
            }
        }

        return output;
    }


    public async Task UpdateUserLastLoginTime(Guid UserId, string? Source = "")
    {
        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId);
        if (rec != null) {
            rec.LastLogin = DateTime.UtcNow;

            if (!String.IsNullOrWhiteSpace(Source)) {
                rec.LastLoginSource = MaxStringLength(Source, 50);
            }

            await data.SaveChangesAsync();

            await SignalRUpdate(new DataObjects.SignalRUpdate { 
                TenantId = rec.TenantId,
                UserId = UserId,
                UpdateType = DataObjects.SignalRUpdateType.LastAccessTime,
                Message = "Updated",
            });
        }
    }

    public async Task<bool> UserCanEditUser(Guid UserId, Guid EditUserId)
    {
        if (UserId == EditUserId) {
            return true;
        }

        DataObjects.User u = await GetUser(UserId);
        if (u.Admin) {
            return true;
        }

        return false;
    }

    public async Task<bool> UserCanViewUser(Guid UserId, Guid ViewUserId)
    {
        DataObjects.User u = await GetUser(UserId);
        if (u.Admin) {
            // An admin or Tech can view any user
            return true;
        }

        DataObjects.User ViewUser = await GetUser(ViewUserId);
        if (ViewUser.UserId == u.UserId) {
            // This is the user's own account
            return true;
        }

        return false;
    }

    public async Task<bool> UserIsMainAdmin(Guid UserId)
    {
        bool output = false;

        var rec = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId && x.Enabled == true && x.Deleted != true);
        if (rec != null) {
            var adminUser = await data.Users.FirstOrDefaultAsync(x => x.TenantId == _guid1 && x.Admin == true && x.Enabled == true && x.Deleted != true &&
                ((x.Username != null && x.Username != "" && x.Username.ToLower() == rec.Username.ToLower())
                ||
                (x.Email != null && x.Email != "" && rec.Email != null && x.Email.ToLower() == rec.Email.ToLower()))
            );
            output = adminUser != null;
        }

        return output;
    }

    public async Task<DataObjects.User> UserSignup(DataObjects.User user)
    {
        DataObjects.User output = user;
        output.ActionResponse = GetNewActionResponse();

        // First, validate the given TenantId
        var tenant = GetTenant(user.TenantId, user);

        if (tenant == null || !tenant.ActionResponse.Result) {
            output.ActionResponse.Messages.Add("Invalid Customer Code");
            return output;
        }

        var appUrl = ApplicationURL;
        string websiteName = WebsiteName(appUrl);
        if (String.IsNullOrWhiteSpace(websiteName)) {
            websiteName = StringValue(appUrl);
        }

        if (String.IsNullOrWhiteSpace(appUrl)) {
            output.ActionResponse.Messages.Add("Application URL Not Configured");
            return output;
        }

        if (!appUrl.EndsWith("/")) {
            appUrl += "/";
        }
        appUrl += tenant.TenantCode + "/";

        // Next, make sure this email address does not already exist for this customer
        var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == user.TenantId && x.Email == user.Email);
        if (rec != null) {
            if (BooleanValue(rec.Deleted)) {
                output.ActionResponse.Messages.Add("A deleted account already exists for the email address you entered. Please contact the application admin for assistance.");
            } else {
                output.ActionResponse.Messages.Add("An account already exists for the email address you entered. Please use the Forgot Password option to reset your password.");
            }
            return output;
        }

        string code = GenerateRandomCode(6);

        string body = "<p>You are receiving this email because you signed up for an account at <strong>" + websiteName + "</strong>.</p>" +
                "<p>Use the following confirmation code on that page to confirm your new account:</p>" +
                "<p style='font-size:2em;'>" + code + "</p>";

        List<string> to = new List<string>();
        to.Add(StringValue(user.Email));

        var settings = GetTenantSettings(user.TenantId);

        string from = String.Empty;
        if (settings != null) {
            from += settings.DefaultReplyToAddress;
        }

        var sent = SendEmail(new DataObjects.EmailMessage {
            From = from,
            To = to,
            Subject = "New User Sign Up at " + websiteName,
            Body = body
        });

        if (sent.Result) {
            output = new DataObjects.User {
                ActionResponse = GetNewActionResponse(true),
                UserId = user.UserId,
                TenantId = user.TenantId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Username = StringValue(user.Email),
                Password = user.Password,
                AuthToken = CompressByteArrayString(Encrypt(code))
            };
        } else {
            output.ActionResponse.Messages.Add("There was an error sending an email to the address you specified.");
        }

        return output;
    }

    public async Task<DataObjects.User> UserSignupConfirm(DataObjects.User user)
    {
        DataObjects.User output = new DataObjects.User();

        if (String.IsNullOrWhiteSpace(user.Email)) {
            output.ActionResponse.Messages.Add("Missing Required Email Address");
        }

        if (String.IsNullOrWhiteSpace(user.Password)) {
            output.ActionResponse.Messages.Add("Missing Required Password");
        }

        if (output.ActionResponse.Messages.Count() == 0) {
            string extended = CompressedByteArrayStringToFullString(user.AuthToken);
            string decrypted = Decrypt(extended);

            if (user.Confirmation == decrypted) {
                // Create the account.
                var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == user.TenantId && x.Email == user.Email);
                if (rec != null) {
                    if (BooleanValue(rec.Deleted)) {
                        output.ActionResponse.Messages.Add("A deleted account already exists for the email address you entered. Please contact the application admin for assistance.");
                    } else {
                        output.ActionResponse.Messages.Add("An account already exists for the email address you entered. Please use the Forgot Password option to reset your password.");
                    }
                    return output;
                }

                try {
                    DateTime now = DateTime.UtcNow;

                    await data.Users.AddAsync(new User {
                        Added = now,
                        Admin = false,
                        Deleted = false,
                        Email = StringValue(user.Email),
                        Enabled = true,
                        FirstName = user.FirstName,
                        LastModified = now,
                        LastName = user.LastName,
                        ManageFiles = false,
                        Password = HashPassword(user.Password),
                        PreventPasswordChange = false,
                        Source = "Local Login Signup Form",
                        TenantId = user.TenantId,
                        UserId = Guid.NewGuid(),
                        Username = StringValue(user.Email),
                    });

                    await data.SaveChangesAsync();
                    output.ActionResponse.Result = true;
                } catch {
                    output.ActionResponse.Messages.Add("An error occurred attempting to create the new user account.");
                }
            } else {
                output.ActionResponse.Messages.Add("Invalid Confirmation Code");
            }
        }

        return output;
    }

    private void ValidateAdminUserExists()
    {
        try {
            // Make sure there is an Admin record in the admin tenant
            bool newRecord = false;
            DateTime now = DateTime.UtcNow;
            string adminPassword = String.Empty;
            var rec = data.Users.FirstOrDefault(x => x.TenantId == _guid1 && x.UserId == _guid1);
            if (rec == null) {
                rec = new EFModels.EFModels.User {
                    Added = now,
                    TenantId = _guid1,
                    UserId = _guid1,
                    PreventPasswordChange = false,
                    ManageFiles = true,
                    LastModified = now,
                };
                newRecord = true;
            }
            rec.Admin = true;
            rec.FirstName = "Admin";
            rec.LastName = "User";
            rec.Email = "admin@local";
            rec.Username = "admin";
            rec.EmployeeId = "app.admin";
            rec.Enabled = true;
            rec.Deleted = false;
            rec.DeletedAt = null;
            rec.PreventPasswordChange = false;
            if (String.IsNullOrEmpty(rec.Password)) {
                adminPassword = "admin";
                rec.Password = HashPassword("admin");
            }

            if (newRecord) {
                data.Users.Add(rec);
            }

            // Next, make sure that an admin user exists in each tenant account using the same password that is set on the admin tenant account.
            var tenants = data.Tenants.Where(x => x.TenantId != _guid1);
            if (tenants != null && tenants.Any()) {
                foreach (var tenant in tenants) {
                    newRecord = false;
                    var tenantAdmin = data.Users.FirstOrDefault(x => x.TenantId == tenant.TenantId && x.Username == "admin");
                    if (tenantAdmin == null) {
                        tenantAdmin = new EFModels.EFModels.User {
                            TenantId = tenant.TenantId,
                            UserId = Guid.NewGuid(),
                            ManageFiles = true,
                            PreventPasswordChange = false
                        };
                        newRecord = true;
                    }
                    tenantAdmin.FirstName = "Admin";
                    tenantAdmin.LastName = "User";
                    tenantAdmin.Email = "admin@local";
                    tenantAdmin.Username = "admin";
                    tenantAdmin.EmployeeId = "app.admin";
                    tenantAdmin.Enabled = true;
                    tenantAdmin.Admin = true;

                    if (!String.IsNullOrWhiteSpace(adminPassword)) {
                        tenantAdmin.Password = HashPassword(adminPassword);
                    }

                    if (newRecord) {
                        data.Users.Add(tenantAdmin);
                    }
                }
            }
            data.SaveChanges();
        } catch { }
    }

    private async Task ValidateMainAdminUser(Guid UserId)
    {
        DateTime now = DateTime.UtcNow;

        var tenants = await GetTenants();
        if (tenants != null && tenants.Any()) {
            var user = await data.Users.FirstOrDefaultAsync(x => x.UserId == UserId);
            if (user != null) {
                foreach (var tenant in tenants.Where(x => x.TenantId != _guid1)) {
                    var rec = await data.Users.FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId
                        && (
                            (x.Username != null && x.Username != "" && x.Username.ToLower() == user.Username.ToLower())
                            ||
                            (x.Email != null && x.Email != "" && x.Email.ToLower() == StringValue(user.Email).ToLower())
                        ));

                    if (rec == null) {
                        rec = new EFModels.EFModels.User {
                            Added = now,
                            UserId = Guid.NewGuid(),
                            TenantId = tenant.TenantId,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            Email = user.Email,
                            Phone = user.Phone,
                            Username = user.Username,
                            EmployeeId = user.EmployeeId,
                            Enabled = true,
                            Admin = false,
                            ManageFiles = user.ManageFiles,
                            Password = user.Password,
                            PreventPasswordChange = false,
                            LastModified = now,
                            Deleted = false,
                            DeletedAt = null,
                        };

                        await data.Users.AddAsync(rec);
                        await data.SaveChangesAsync();
                    }
                }
            }
        }
    }

    public async Task<DataObjects.BooleanResponse> ValidateSelectedUserAccount(Guid TenantId, Guid UserId, DataObjects.User? CurrentUser = null)
    {
        DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();
        // See if this is an existing user
        DataObjects.User user = await GetUser(UserId, false, CurrentUser);
        if (user.ActionResponse.Result) {
            output.Result = true;
            output.Messages.Add("User Already Exists");
            return output;
        }

        if (!output.Result && output.Messages.Count() == 0) {
            output.Messages.Add("Unable to Validate UserId '" + UserId.ToString() + "'");
        }
        return output;
    }
}