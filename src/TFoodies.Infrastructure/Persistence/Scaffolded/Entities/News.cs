using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class News
{
    [Key]
    public Guid newid { get; set; }

    [StringLength(100)]
    public string title { get; set; } = null!;

    [StringLength(150)]
    public string? summary { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    [Column(TypeName = "ntext")]
    public string intro { get; set; } = null!;

    [StringLength(50)]
    public string? activitydate { get; set; }

    [StringLength(50)]
    public string? activityschedule { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime publishdate { get; set; }

    [StringLength(150)]
    public string? shortener { get; set; }

    [InverseProperty("_new")]
    public virtual ICollection<Newmedia> Newmedia { get; set; } = new List<Newmedia>();
}
