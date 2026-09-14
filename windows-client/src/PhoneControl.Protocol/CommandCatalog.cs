namespace PhoneControl.Protocol;

public static class CommandCatalog
{
    public static bool CanExecute(string type)
    {
        return MessageTypes.IsKnown(type);
    }
}
