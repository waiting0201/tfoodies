using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Newmedia
{
    [Key]
    public Guid newmediaid { get; set; }

    public Guid newid { get; set; }

    public int mediatype { get; set; }

    [StringLength(150)]
    public string? videourl { get; set; }

    [StringLength(50)]
    public string? filename { get; set; }

    [StringLength(250)]
    public string? filenamepath { get; set; }

    [StringLength(50)]
    public string? photo { get; set; }

    [ForeignKey("newid")]
    [InverseProperty("Newmedia")]
    public virtual News _new { get; set; } = null!;
}
