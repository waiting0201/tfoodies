using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Eventphoto
{
    [Key]
    public Guid eventphotoid { get; set; }

    public Guid eventid { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    public int sort { get; set; }

    [ForeignKey("eventid")]
    [InverseProperty("Eventphotos")]
    public virtual Event _event { get; set; } = null!;
}
