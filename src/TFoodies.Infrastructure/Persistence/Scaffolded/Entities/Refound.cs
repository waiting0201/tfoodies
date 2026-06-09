using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Refound
{
    [Key]
    public Guid refoundid { get; set; }

    public Guid memberid { get; set; }

    public Guid returnid { get; set; }

    [StringLength(15)]
    public string refoundcode { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime refounddate { get; set; }

    public int amount { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [ForeignKey("returnid")]
    [InverseProperty("Refounds")]
    public virtual Return _return { get; set; } = null!;

    [ForeignKey("memberid")]
    [InverseProperty("Refounds")]
    public virtual Member member { get; set; } = null!;
}
