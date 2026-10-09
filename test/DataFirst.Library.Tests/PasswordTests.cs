using DataFirst.Testing;
using DataFirst.Library;
using DataFirst.Lodash;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataFirst.Library.Tests;

public sealed class PasswordTests
{
    private static readonly Gen<string> Password =
        Gen.Choose(1, 12).SelectMany(n => Gen.Elements("abcXYZ019 !é☃".ToCharArray()).ArrayOf(n).Select(c => new string(c)));

    [Property(MaxTest = 25)]
    public Property A_password_verifies_against_its_own_hash() =>
        Prop.ForAll(Password.ToArbitrary(), p => Passwords.Verify(Passwords.Hash(p, 1), p));

    [Property(MaxTest = 25)]
    public Property A_different_password_does_not_verify() =>
        Prop.ForAll(Password.Zip(Password).Where(t => t.Item1 != t.Item2).ToArbitrary(), t =>
            !Passwords.Verify(Passwords.Hash(t.Item1, 1), t.Item2));

    [Property(MaxTest = 25)]
    public Property Hashing_the_same_password_twice_gives_different_salts_and_hashes() =>
        Prop.ForAll(Password.ToArbitrary(), p =>
        {
            var (one, two) = (Passwords.Hash(p, 1), Passwords.Hash(p, 1));
            return _.Get<string>(one, "salt") != _.Get<string>(two, "salt")
                   && _.Get<string>(one, "hash") != _.Get<string>(two, "hash");
        });

    [Property(MaxTest = 25)]
    public Property The_record_never_contains_the_password() =>
        Prop.ForAll(Password.Where(p => p.Length >= 4).ToArbitrary(), p =>
            !Passwords.Hash(p, 1).ToString().Contains(p) && !Passwords.Hash(p, 1).ToString().Contains(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(p))));

    [Property(MaxTest = 25)]
    public Property The_iteration_count_travels_with_the_hash_and_is_honoured(PositiveInt seed) =>
        Prop.ForAll(Gen.Choose(1, 50).ToArbitrary(), iterations =>
        {
            var record = Passwords.Hash("secret", iterations);
            var altered = record.SetItem("iterations", (long)iterations + 1);
            return _.Get<long>(record, "iterations") == iterations
                   && Passwords.Verify(record, "secret")
                   && !Passwords.Verify(altered, "secret");
        });

    [Fact]
    public void The_hash_is_a_map_with_salt_hash_and_iterations_that_fits_the_member_schema()
    {
        var record = Passwords.Hash("secret", 1);

        record.Keys.Should().Equal("salt", "hash", "iterations");
        Convert.FromBase64String(_.Get<string>(record, "salt")).Should().HaveCount(16);
        Convert.FromBase64String(_.Get<string>(record, "hash")).Should().HaveCount(32);
        Validation.Validate(Schemas.Member, Map.Of(("email", "a@b.co"), ("password", record))).IsValid().Should().BeTrue();
    }

    [Fact]
    public void The_default_work_factor_is_high()
    {
        _.Get<long>(Passwords.Hash("secret"), "iterations").Should().BeGreaterThanOrEqualTo(210_000);
    }

    [Fact]
    public void An_empty_password_cannot_be_hashed_or_verified()
    {
        new Action(() => Passwords.Hash("", 1)).Should().Throw<ArgumentException>();
        new Action(() => Passwords.Hash(null!, 1)).Should().Throw<ArgumentException>();
        Passwords.Verify(Passwords.Hash("x", 1), "").Should().BeFalse();
        Passwords.Verify(Passwords.Hash("x", 1), null!).Should().BeFalse();
    }

    [Fact]
    public void Passwords_are_case_and_whitespace_sensitive()
    {
        var record = Passwords.Hash("Secret", 1);

        Passwords.Verify(record, "secret").Should().BeFalse();
        Passwords.Verify(record, "Secret ").Should().BeFalse();
        Passwords.Verify(record, "Secret").Should().BeTrue();
    }

    [Fact]
    public void A_credential_that_is_not_a_map_is_a_failed_login()
    {
        Passwords.Verify(DataNull.Instance, "x").Should().BeFalse();
        Passwords.Verify("plaintext", "plaintext").Should().BeFalse();
        Passwords.Verify(DataList.Empty, "x").Should().BeFalse();
    }

    [Fact]
    public void A_malformed_credential_record_is_a_failed_login_not_a_crash()
    {
        var good = Passwords.Hash("x", 1);

        Passwords.Verify(DataMap.Empty, "x").Should().BeFalse();
        Passwords.Verify(good.Remove("salt"), "x").Should().BeFalse();
        Passwords.Verify(good.Remove("hash"), "x").Should().BeFalse();
        Passwords.Verify(good.Remove("iterations"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("salt", "***not base64***"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("hash", "***not base64***"), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("salt", 5), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("iterations", "many"), "x").Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_record_claiming_no_iterations_is_a_failed_login_not_a_crash(int iterations)
    {
        var record = Passwords.Hash("x", 1).SetItem("iterations", (long)iterations);
        Passwords.Verify(record, "x").Should().BeFalse();
    }

    [Fact]
    public void A_truncated_stored_hash_never_matches()
    {
        var good = Passwords.Hash("x", 1);
        var hash = Convert.FromBase64String(_.Get<string>(good, "hash"));

        Passwords.Verify(good.SetItem("hash", Convert.ToBase64String(hash[..16])), "x").Should().BeFalse();
        Passwords.Verify(good.SetItem("hash", ""), "x").Should().BeFalse();
    }
}
