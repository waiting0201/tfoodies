using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Admin
{
    [Key]
    public int AdminID { get; set; }

    [StringLength(50)]
    public string Username { get; set; } = null!;

    [StringLength(20)]
    public string Password { get; set; } = null!;

    public byte Isenable { get; set; }

    [InverseProperty("Admin")]
    public virtual ICollection<AdminLim> AdminLims { get; set; } = new List<AdminLim>();
}
