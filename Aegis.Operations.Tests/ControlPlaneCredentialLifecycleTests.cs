using Common.Security.Hosting;
using Common.Security.Models;
using Xunit;

public sealed class ControlPlaneCredentialLifecycleTests
{
    private static SuiteIdentity Administrator()=>new("administrator","Administrator","Administrator",["Security.Manage"],
        "approved-admin-id",1,new ExternalIdentityKey("directory","domain","administrator"),AuthenticationProviderKind.ActiveDirectory,DateTimeOffset.UtcNow);

    [Fact]
    public async Task LoopbackAndPublicRegistrationIdentifiersCannotEnrollAMachine()
    {
        string directory=Path.Combine(Path.GetTempPath(),"operations-enrollment-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new OperationsEndpoints.ControlPlaneRegistrationStore(directory);
            var denied=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",null,null,true,default);
            Assert.False(denied.Succeeded);Assert.Null(denied.Credential);Assert.Equal(0,await store.CountRegisteredAsync(default));
            Assert.Empty(Directory.GetFiles(directory,"*.json"));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

    [Fact]
    public async Task ARevokedCredentialCannotReenrollItselfAndAValidCredentialIsBoundToItsInstanceAndLoopback()
    {
        string directory=Path.Combine(Path.GetTempPath(),"operations-reenrollment-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new OperationsEndpoints.ControlPlaneRegistrationStore(directory);
            var first=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",Administrator(),null,false,default);
            var rotated=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",null,first.Credential,true,default);
            Assert.True(rotated.Succeeded);
            Assert.False((await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",null,first.Credential,true,default)).Succeeded);
            Assert.False((await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","other-instance",null,rotated.Credential,true,default)).Succeeded);
            Assert.False((await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",null,rotated.Credential,false,default)).Succeeded);
            Assert.Equal(1,await store.CountRegisteredAsync(default));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

    [Fact]
    public async Task RotationRevokesPreviousProofAndPreservesRegisteredApplicationIdentity()
    {
        string directory=Path.Combine(Path.GetTempPath(),"operations-security-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new OperationsEndpoints.ControlPlaneRegistrationStore(directory);
            var original=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",Administrator(),null,false,default);
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Valid,await store.ValidateAsync("Aegis.Configuration","instance",original.Credential!,default));
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Invalid,await store.ValidateAsync("Aegis.Configuration","instance","wrong-credential",default));
            var replacement=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",Administrator(),null,false,default);
            Assert.NotEqual(original.Credential,replacement.Credential);
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Revoked,await store.ValidateAsync("Aegis.Configuration","instance",original.Credential!,default));
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Valid,await store.ValidateAsync("Aegis.Configuration","instance",replacement.Credential!,default));
            Assert.Equal(1,await store.CountRegisteredAsync(default));
            foreach(var file in Directory.GetFiles(directory,"*.json")) {string content=await File.ReadAllTextAsync(file);Assert.DoesNotContain(original.Credential!,content);Assert.DoesNotContain(replacement.Credential!,content);}
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
