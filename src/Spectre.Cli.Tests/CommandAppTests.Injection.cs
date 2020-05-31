using Shouldly;
using Spectre.Cli.Testing;
using Spectre.Cli.Testing.Data.Commands;
using Xunit;

namespace Spectre.Cli.Tests
{
    public sealed partial class CommandAppTests
    {
        public sealed class Injection
        {
            public sealed class FakeDependency
            {
            }

            public sealed class InjectSettings : CommandSettings
            {
                public FakeDependency Fake { get; set; }

                [CommandOption("--name <NAME>")]
                public string Name { get; }

                [CommandOption("--age <AGE>")]
                public int Age { get; set; }

                public InjectSettings(FakeDependency fake, string name)
                {
                    Fake = fake;
                    Name = "Hello " + name;
                }
            }

            [Fact]
            public void Should_Inject_Parameters()
            {
                // Given
                var app = new CommandAppFixture();
                var dependency = new FakeDependency();

                app.WithDefaultCommand<GenericCommand<InjectSettings>>();
                app.Configure(config =>
                {
                    config.Settings.Registrar.RegisterInstance(dependency);
                    config.PropagateExceptions();
                });

                // When
                var (result, _, _, settings) = app.Run(new[]
                {
                    "--name", "foo",
                    "--age", "35",
                });

                // Then
                result.ShouldBe(0);
                settings.ShouldBeOfType<InjectSettings>().And(injected =>
                {
                    injected.ShouldNotBeNull();
                    injected.Fake.ShouldBeSameAs(dependency);
                    injected.Name.ShouldBe("Hello foo");
                    injected.Age.ShouldBe(35);
                });
            }
        }
    }
}
