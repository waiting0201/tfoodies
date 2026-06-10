using System.Data;
using Dapper;

namespace TFoodies.Infrastructure.Persistence;

/// <summary>
/// Dapper 在此 runtime（.NET 10 / Dapper 2.1.79 / Microsoft.Data.SqlClient）無法將
/// <see cref="DateOnly"/> 直接當作 SQL 參數值（會丟
/// "The member ... of type System.DateOnly cannot be used as a parameter value"）。
/// 註冊本 handler 後，<c>DateOnly</c> 與 <c>DateOnly?</c> 皆可作為寫入參數（對應 SQL <c>date</c> 欄位），
/// 並可從 <c>date</c> 欄位讀回。Dapper 的 AddTypeHandler 會自動一併涵蓋 Nullable 版本。
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        string s => DateOnly.Parse(s),
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value)),
    };

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
