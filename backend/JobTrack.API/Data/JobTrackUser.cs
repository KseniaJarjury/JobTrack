using Microsoft.AspNetCore.Identity;

namespace JobTrack.API.Data;

public sealed class JobTrackUser : IdentityUser
{
    public int AccountSlot { get; set; } = 1;
}
