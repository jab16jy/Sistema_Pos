using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using POS.Application.UseCases.Roles;

namespace POS.Presentation.Converters;

public sealed class RoleIdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is Guid roleId && values[1] is IEnumerable<RoleSummary> roles)
        {
            var role = roles.FirstOrDefault(r => r.Id == roleId);
            return role is null ? "Sin Rol" : RoleDisplayNames.ToDisplayName(role.Name);
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
