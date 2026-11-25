using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Finds the rows open around a row
    /// </summary>
    internal static class Enclosing
    {
        /// <summary>
        /// The row open around <paramref name="row"/> of type <typeparamref name="TRow"/>, innermost, if any
        /// </summary>
        public static ContextRow? Row<TRow>(ContextRow row, DeserializeContext context) where TRow : Row =>
            context.Ancestors(row).FirstOrDefault(ancestor => ancestor.Row is TRow);
    }

    /// <summary>
    /// A sub-object never comes out; it takes its object's key and is kept until the document ends
    /// </summary>
    internal sealed class SubObjectPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">The sub-object is not inside an object with a key</exception>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            ContextRow? definition = Enclosing.Row<ObjectDefinition>(row, context);

            if (definition is null || !definition.IsSet(nameof(ObjectDefinition.ObjectKey)))
            {
                throw new InvalidDataException($"SubObject {row.Row.AsType<SubObject>().SubObjectKey} is not inside an {nameof(ObjectDefinition)} with an {nameof(ObjectDefinition.ObjectKey)} before it.");
            }

            row.Set(nameof(SubObject.ObjectKey), definition.Row.AsType<ObjectDefinition>().ObjectKey);
            row.Enumerate(false);
            row.Keep(Lifetime.UntilEnd);

            next(row);
        }
    }

    /// <summary>
    /// Contact information never comes out; an operator's business name is copied onto its
    /// <see cref="OperatorDefinition"/>, any other is ignored
    /// </summary>
    internal sealed class ContactInformationPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (context.Parent(row) is { Row: OperatorDefinition } definition)
            {
                definition.Set(nameof(OperatorDefinition.OperatorBusinessName), row.Row.AsType<ContactInformation>().BusinessName);
            }

            next(row);
        }
    }

    /// <summary>
    /// A classified product definition never comes out; its identifiers are copied onto the enclosing
    /// <see cref="ProductDefinition"/>
    /// </summary>
    internal sealed class ClassifiedProductDefinitionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (Enclosing.Row<ProductDefinition>(row, context) is { } definition)
            {
                ClassifiedProductDefinition classified = row.Row.AsType<ClassifiedProductDefinition>();

                definition.Set(nameof(ProductDefinition.ProductName), classified.ProductName);
                definition.Set(nameof(ProductDefinition.ProductInfo), classified.ProductInfo);
                definition.Set(nameof(ProductDefinition.ProductVersion), classified.ProductVersion);
                definition.Set(nameof(ProductDefinition.ProductUserID), classified.ProductUserID);
            }

            next(row);
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
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (Enclosing.Row<ProductDefinition>(row, context) is { } definition)
            {
                if (definition.IsSet(nameof(ProductDefinition.ProductDestinationBusinessName)))
                {
                    throw new InvalidDataException($"{nameof(ProductDefinition)} {definition.Row.AsType<ProductDefinition>().ProductKey} has more than one {nameof(ProductDestination)}.");
                }

                ProductDestination destination = row.Row.AsType<ProductDestination>();

                definition.Set(nameof(ProductDefinition.ProductDestinationBusinessName), destination.BusinessName);
                definition.Set(nameof(ProductDefinition.ProductDestinationBusinessID), destination.BusinessID);
            }

            next(row);
        }
    }

    /// <summary>
    /// A logging contractor never comes out; its business identity is copied onto the <see cref="Machine"/>
    /// </summary>
    internal sealed class LoggingContractorPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);

            if (Enclosing.Row<Machine>(row, context) is { } machine)
            {
                machine.Set(nameof(Machine.LoggingContractorBusinessID), row.Row.AsType<LoggingContractor>().BusinessID);
            }

            next(row);
        }
    }
}
