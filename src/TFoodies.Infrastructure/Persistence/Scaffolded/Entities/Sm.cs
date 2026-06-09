using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Sm
{
    [Key]
    public Guid smsid { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    /// <summary>
    /// 簡訊內容
    /// </summary>
    [StringLength(160)]
    public string smbody { get; set; } = null!;

    /// <summary>
    /// 預約發送時間
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? dlvtime { get; set; }

    [InverseProperty("sms")]
    public virtual ICollection<Smsdetail> Smsdetails { get; set; } = new List<Smsdetail>();
}
