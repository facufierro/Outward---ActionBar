using System;

/// <summary>
/// Attributes for BepInEx ConfigurationManager.
/// Include this class to use CustomDrawer and other display options.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ConfigurationManagerAttributes : Attribute
{
    public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
    public bool? HideDefaultButton;
    public bool? Browsable;
    public bool? IsAdvanced;
    public int? Order;
}
