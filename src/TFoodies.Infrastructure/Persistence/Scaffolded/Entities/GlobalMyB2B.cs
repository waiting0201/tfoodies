using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

[Table("GlobalMyB2B")]
public partial class GlobalMyB2B
{
    [Key]
    public Guid globalmyb2bid { get; set; }

    [StringLength(5)]
    public string error_id { get; set; } = null!;

    [StringLength(50)]
    public string? error_msg { get; set; }

    [Column(TypeName = "ntext")]
    public string? remark { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }
}
