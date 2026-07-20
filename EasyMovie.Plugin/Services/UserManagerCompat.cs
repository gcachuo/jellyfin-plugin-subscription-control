using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;

namespace EasyMovie.Plugin.Services;

public static class UserManagerCompat
{
    public static IEnumerable<User> GetUsers(IUserManager userManager)
    {
        var type = userManager.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var usersProperty = type.GetProperty("Users", flags);
        if (usersProperty?.GetValue(userManager) is IEnumerable<User> propertyUsers)
        {
            return propertyUsers;
        }

        var getUsersMethod = type.GetMethod("GetUsers", flags, Type.DefaultBinder, Type.EmptyTypes, null);
        if (getUsersMethod?.Invoke(userManager, Array.Empty<object>()) is IEnumerable<User> methodUsers)
        {
            return methodUsers;
        }

        return Enumerable.Empty<User>();
    }
}
