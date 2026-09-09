using Kapea.Application.Identity;
using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Application.Tests.Identity;

public class DevelopmentDataAdoptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId DevelopmentUser = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task The_only_user_adopts_what_the_development_user_had()
    {
        var repository = new FakeUserRepository(users: 1);
        var user = User.Register("Google", "sujeto", "Nacho", null, Now);

        await Adoption(repository).AdoptAsync(user, DevelopmentUser);

        Assert.Equal((DevelopmentUser, user.Id), repository.Reassigned);
    }

    [Fact]
    public async Task With_more_than_one_user_nothing_is_adopted()
    {
        // Con dos personas en el registro, adoptar dejaría que una se llevara datos que
        // podrían ser de la otra. La regla solo es inocua mientras haya una sola.
        var repository = new FakeUserRepository(users: 2);
        var user = User.Register("Google", "sujeto", "Nacho", null, Now);

        await Adoption(repository).AdoptAsync(user, DevelopmentUser);

        Assert.Null(repository.Reassigned);
    }

    [Fact]
    public async Task Adopting_from_oneself_does_nothing()
    {
        var repository = new FakeUserRepository(users: 1);
        var user = User.Register("Google", "sujeto", "Nacho", null, Now);

        await Adoption(repository).AdoptAsync(user, user.Id);

        Assert.Null(repository.Reassigned);
    }

    private static DevelopmentDataAdoption Adoption(FakeUserRepository repository) =>
        new(repository, NullLogger<DevelopmentDataAdoption>.Instance);

    private sealed class FakeUserRepository(int users) : IUserRepository
    {
        internal (UserId From, UserId To)? Reassigned { get; private set; }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(users);

        public Task<int> ReassignOwnershipAsync(UserId from, UserId to, CancellationToken cancellationToken = default)
        {
            Reassigned = (from, to);

            return Task.FromResult(1);
        }

        public Task<User?> FindByIdentityAsync(string provider, string subject, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> FindAsync(UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
