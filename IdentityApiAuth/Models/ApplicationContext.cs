using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityApiAuth.Models;

public class ApplicationContext : IdentityDbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> opts) : base(opts) {}
}