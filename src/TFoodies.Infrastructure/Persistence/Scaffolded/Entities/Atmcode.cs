using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Atmcode
{
    [Key]
    public Guid atmcodeid { get; set; }

    [StringLength(4)]
    public string year { get; set; } = null!;

    [StringLength(2)]
    public string month { get; set; } = null!;

    [StringLength(2)]
    public string day { get; set; } = null!;

    public int code { get; set; }
}
