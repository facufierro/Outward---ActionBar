using System;
using UnityEngine;

/// <summary>
/// Attributes for use with BepInEx.ConfigurationManager
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ConfigurationManagerAttributes : Attribute
{
    /// <summary>
    /// Show this setting in the configuration manager even if it is an advanced setting
    /// </summary>
    public bool? ShowRangeAsPercent;

    /// <summary>
    /// Custom drawing code for the setting.
    /// func(BepInEx.Configuration.ConfigEntryBase entry)
    /// </summary>
    public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

    /// <summary>
    /// Show this setting in the configuration manager even if it is an advanced setting
    /// </summary>
    public bool? Browsable;

    /// <summary>
    /// Category for the setting
    /// </summary>
    public string Category;

    /// <summary>
    /// Default value for the setting
    /// </summary>
    public object DefaultValue;

    /// <summary>
    /// Hides the setting from the configuration manager
    /// </summary>
    public bool? HideDefaultButton;

    /// <summary>
    /// Hides the setting from the configuration manager
    /// </summary>
    public bool? HideSettingName;
    
    /// <summary>
    /// Description of the setting
    /// </summary>
    public string Description;

    /// <summary>
    /// Sort order of the setting
    /// </summary>
    public int? Order;
    
    /// <summary>
    /// If true, the setting will be read-only in the configuration manager
    /// </summary>
    public bool? ReadOnly;

    /// <summary>
    /// If true, the setting will be shown as a slider in the configuration manager
    /// </summary>
    public bool? IsAdvanced;
}
