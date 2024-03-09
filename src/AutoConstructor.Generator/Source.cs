namespace AutoConstructor.Generator;

public static class Source
{
    internal const string AttributeFullName = "AutoConstructorAttribute";

    internal const string AttributeText = $$"""
        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by the {{nameof(AutoConstructor)}} source generator.
        //
        //     Changes to this file may cause incorrect behavior and will be lost if
        //     the code is regenerated.
        // </auto-generated>
        //------------------------------------------------------------------------------

        /// <summary>
        /// Add automatic constructor generation to the class.
        /// </summary>
        /// <example>
        /// [AutoConstructor]
        /// public partial class MyClass
        /// {
        ///     private readonly MyDbContext _context;
        ///     private readonly IHttpClientFactory _clientFactory;
        ///     private readonly IService _service;
        /// }
        /// </example>
        /// <seealso href="https://github.com/k94ll13nn3/AutoConstructor?tab=readme-ov-file#basic-usage"/>
        [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
        internal sealed class {{AttributeFullName}} : System.Attribute
        {
            /// <summary>
            /// Add automatic constructor generation to the class.
            /// </summary>
            /// <param name="accessibility">Configure the accessibility of the constructor, public by default</param>
            public {{AttributeFullName}}(string accessibility = null)
            {
                Accessibility = accessibility;
            }

            public string Accessibility { get; }
        }

        """;

    internal const string IgnoreAttributeFullName = "AutoConstructorIgnoreAttribute";

    internal const string IgnoreAttributeText = $$"""
        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by the {{nameof(AutoConstructor)}} source generator.
        //
        //     Changes to this file may cause incorrect behavior and will be lost if
        //     the code is regenerated.
        // </auto-generated>
        //------------------------------------------------------------------------------

        /// <summary>
        /// Exclude field from being injected into the generated constructor.
        /// </summary>
        /// <example>
        /// [AutoConstructor]
        /// public partial class MyClass
        /// {
        ///     [AutoConstructorIgnore]
        ///     private readonly MyDbContext _context;
        /// }
        /// </example>
        [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        internal sealed class {{IgnoreAttributeFullName}} : System.Attribute
        {
        }

        """;

    internal const string InjectAttributeFullName = "AutoConstructorInjectAttribute";

    internal const string InjectAttributeText = $$"""
        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by the {{nameof(AutoConstructor)}} source generator.
        //
        //     Changes to this file may cause incorrect behavior and will be lost if
        //     the code is regenerated.
        // </auto-generated>
        //------------------------------------------------------------------------------

        /// <summary>
        /// Modify the way that the field is injected into the constructor.
        /// </summary>
        /// <example>
        /// [AutoConstructor]
        /// public partial class MyClass
        /// {
        ///     [AutoConstructorInject("guid.ToString().Length", "guid", typeof(Guid))]
        ///     private readonly int _guidLength;
        /// }
        /// </example>
        [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        internal sealed class {{InjectAttributeFullName}} : System.Attribute
        {
            /// <summary>
            /// Initialize the attribute
            /// </summary>
            /// <param name="initializer"> A string that will be used to initialize the field, default to the parameterName if null or empty.</param>
            /// <param name="parameterName">The name of the parameter to used in the constructor, default to the field name trimmed if null or empty.</param>
            /// <param name="injectedType">The type of the parameter to used in the constructor, default to the field type if null.</param>
            public {{InjectAttributeFullName}}(string initializer = null, string parameterName = null, System.Type injectedType = null)
            {
                Initializer = initializer;
                ParameterName = parameterName;
                InjectedType = injectedType;
            }

            public string Initializer { get; }

            public string ParameterName { get; }

            public System.Type InjectedType { get; }
        }

        """;

    internal const string InitializerAttributeFullName = "AutoConstructorInitializerAttribute";

    internal const string InitializerAttributeText = $$"""
        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by the {{nameof(AutoConstructor)}} source generator.
        //
        //     Changes to this file may cause incorrect behavior and will be lost if
        //     the code is regenerated.
        // </auto-generated>
        //------------------------------------------------------------------------------

        /// <summary>
        /// Configure a method to be called by the generated constructor. The method must be parameterless and returning void.
        /// </summary>
        /// <example>
        /// [AutoConstructor]
        /// internal partial class Test
        /// {
        ///     private readonly int _t;
        /// 
        ///     [AutoConstructorInitializer]
        ///     public void Initializer()
        ///     {
        ///     }
        /// }
        /// </example>
        /// <seealso href="https://github.com/k94ll13nn3/AutoConstructor?tab=readme-ov-file#initializer-method"/>
        [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
        internal sealed class {{InitializerAttributeFullName}} : System.Attribute
        {
        }

        """;
}
