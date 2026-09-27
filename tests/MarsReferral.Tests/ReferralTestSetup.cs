using MarsReferral.Core;

internal static class ReferralTestSetup
{
    public static int Fund(ReferralService service, int customerId)
    {
        var operations = new Actor(IsOperations: true);
        var application = service.CreateApplication(operations, customerId, "Home purchase", 200000);
        service.Transition(operations, application.Id, ApplicationStage.Approved);
        service.Transition(operations, application.Id, ApplicationStage.Funded);
        return application.Id;
    }
}
