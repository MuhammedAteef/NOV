using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace SystemManagementSystem.Models;

public partial class VerificationCode
{
    public int Id { get; set; }

    public string UserId { get; set; }

    public string Code { get; set; }

    public string Type { get; set; }

    public DateTime ExpirationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; } = null;

    public virtual IdentityUser User { get; set; } = null!;

}