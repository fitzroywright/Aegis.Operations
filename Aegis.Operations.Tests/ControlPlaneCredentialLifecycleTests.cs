using Xunit;

public sealed class ControlPlaneCredentialLifecycleTests
{
    [Fact]
    public async Task RotationRevokesPreviousProofAndPreservesRegisteredApplicationIdentity()
    {
        string directory=Path.Combine(Path.GetTempPath(),"operations-security-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new OperationsEndpoints.ControlPlaneRegistrationStore(directory);
            var original=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",default);
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Valid,await store.ValidateAsync("Aegis.Configuration","instance",original.Credential!,default));
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Invalid,await store.ValidateAsync("Aegis.Configuration","instance","wrong-credential",default));
            var replacement=await store.ProvisionControlPlaneAsync("Aegis.Configuration","Configuration","instance",default);
            Assert.NotEqual(original.Credential,replacement.Credential);
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Revoked,await store.ValidateAsync("Aegis.Configuration","instance",original.Credential!,default));
            Assert.Equal(OperationsEndpoints.RegistrationValidation.Valid,await store.ValidateAsync("Aegis.Configuration","instance",replacement.Credential!,default));
            Assert.Equal(1,await store.CountRegisteredAsync(default));
            foreach(var file in Directory.GetFiles(directory,"*.json")) {string content=await File.ReadAllTextAsync(file);Assert.DoesNotContain(original.Credential!,content);Assert.DoesNotContain(replacement.Credential!,content);}
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
