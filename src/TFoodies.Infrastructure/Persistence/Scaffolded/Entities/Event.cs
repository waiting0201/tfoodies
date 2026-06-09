using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Event
{
    [Key]
    public Guid eventid { get; set; }

    [StringLength(100)]
    public string title { get; set; } = null!;

    [StringLength(150)]
    public string summary { get; set; } = null!;

    [Column(TypeName = "ntext")]
    public string intro { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime eventdate { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [StringLength(150)]
    public string? shortener { get; set; }

    public int sort { get; set; }

    [InverseProperty("_event")]
    public virtual ICollection<Eventphoto> Eventphotos { get; set; } = new List<Eventphoto>();
}
