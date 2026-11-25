using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Finds the telemetries open around a telemetry
    /// </summary>
    internal static class Enclosing
    {
        /// <summary>
        /// The telemetry open around <paramref name="telemetry"/> of type <typeparamref name="TTelemetry"/>, innermost, if any
        /// </summary>
        public static ContextTelemetry? Telemetry<TTelemetry>(ContextTelemetry telemetry, DeserializeContext context) where TTelemetry : Telemetry =>
            context.Ancestors(telemetry).FirstOrDefault(ancestor => ancestor.Telemetry is TTelemetry);
    }

    /// <summary>
    /// A sub-object never comes out; it takes its object's key and is kept until the document ends
    /// </summary>
    internal sealed class SubObjectPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">The sub-object is not inside an object with a key</exception>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            ContextTelemetry? definition = Enclosing.Telemetry<ObjectDefinition>(telemetry, context);

            if (definition is null || !definition.IsSet(nameof(ObjectDefinition.ObjectKey)))
            {
                throw new InvalidDataException($"SubObject {telemetry.Telemetry.AsType<SubObject>().SubObjectKey} is not inside an {nameof(ObjectDefinition)} with an {nameof(ObjectDefinition.ObjectKey)} before it.");
            }

            telemetry.Set(nameof(SubObject.ObjectKey), definition.Telemetry.AsType<ObjectDefinition>().ObjectKey);
            telemetry.Enumerate(false);
            telemetry.Keep(Lifetime.UntilEnd);

            next(telemetry);
        }
    }

    /// <summary>
    /// Contact information never comes out; an operator's business name is copied onto its
    /// <see cref="OperatorDefinition"/>, any other is ignored
    /// </summary>
    internal sealed class ContactInformationPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            telemetry.Enumerate(false);

            if (context.Parent(telemetry) is { Telemetry: OperatorDefinition } definition)
            {
                definition.Set(nameof(OperatorDefinition.OperatorBusinessName), telemetry.Telemetry.AsType<ContactInformation>().BusinessName);
            }

            next(telemetry);
        }
    }

    /// <summary>
    /// A classified product definition never comes out; its identifiers are copied onto the enclosing
    /// <see cref="ProductDefinition"/>
    /// </summary>
    internal sealed class ClassifiedProductDefinitionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            telemetry.Enumerate(false);

            if (Enclosing.Telemetry<ProductDefinition>(telemetry, context) is { } definition)
            {
                ClassifiedProductDefinition classified = telemetry.Telemetry.AsType<ClassifiedProductDefinition>();

                definition.Set(nameof(ProductDefinition.ProductName), classified.ProductName);
                definition.Set(nameof(ProductDefinition.ProductInfo), classified.ProductInfo);
                definition.Set(nameof(ProductDefinition.ProductVersion), classified.ProductVersion);
                definition.Set(nameof(ProductDefinition.ProductUserID), classified.ProductUserID);
            }

            next(telemetry);
        }
    }

    /// <summary>
    /// A product destination never comes out; it is copied onto the enclosing <see cref="ProductDefinition"/>
    /// </summary>
    internal sealed class ProductDestinationPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">A product with a second destination (S0: a value is never
        /// silently replaced)</exception>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            telemetry.Enumerate(false);

            if (Enclosing.Telemetry<ProductDefinition>(telemetry, context) is { } definition)
            {
                if (definition.IsSet(nameof(ProductDefinition.ProductDestinationBusinessName)))
                {
                    throw new InvalidDataException($"{nameof(ProductDefinition)} {definition.Telemetry.AsType<ProductDefinition>().ProductKey} has more than one {nameof(ProductDestination)}.");
                }

                ProductDestination destination = telemetry.Telemetry.AsType<ProductDestination>();

                definition.Set(nameof(ProductDefinition.ProductDestinationBusinessName), destination.BusinessName);
                definition.Set(nameof(ProductDefinition.ProductDestinationBusinessID), destination.BusinessID);
            }

            next(telemetry);
        }
    }

    /// <summary>
    /// A logging contractor never comes out; its business identity is copied onto the <see cref="Machine"/>
    /// </summary>
    internal sealed class LoggingContractorPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            telemetry.Enumerate(false);

            if (Enclosing.Telemetry<Machine>(telemetry, context) is { } machine)
            {
                machine.Set(nameof(Machine.LoggingContractorBusinessID), telemetry.Telemetry.AsType<LoggingContractor>().BusinessID);
            }

            next(telemetry);
        }
    }
}
