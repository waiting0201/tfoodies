using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Viewlog
{
    [Key]
    public Guid viewlogid { get; set; }

    [StringLength(150)]
    public string sessionid { get; set; } = null!;

    [StringLength(150)]
    public string? referrersessionid { get; set; }

    public Guid? memberid { get; set; }

    [StringLength(50)]
    public string? country { get; set; }

    [StringLength(50)]
    public string? city { get; set; }

    [StringLength(50)]
    public string? browser { get; set; }

    [StringLength(50)]
    public string? device { get; set; }

    [StringLength(50)]
    public string? platform { get; set; }

    [Column(TypeName = "ntext")]
    public string? url { get; set; }

    [StringLength(50)]
    public string? referrerdns { get; set; }

    [Column(TypeName = "ntext")]
    public string? referrerurl { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }
}
