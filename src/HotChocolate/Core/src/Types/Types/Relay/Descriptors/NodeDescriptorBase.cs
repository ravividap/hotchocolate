using System;
using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Properties;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;
using HotChocolate.Utilities;

#nullable enable

namespace HotChocolate.Types.Relay.Descriptors
{
    public abstract class NodeDescriptorBase : DescriptorBase<NodeDefinition>
    {
        protected NodeDescriptorBase(IDescriptorContext context)
            : base(context)
        {
        }

        protected internal sealed override NodeDefinition Definition { get; protected set; } =
            new();

        protected abstract IObjectFieldDescriptor ConfigureNodeField();

        public virtual IObjectFieldDescriptor ResolveNode(
            FieldResolverDelegate fieldResolver)
        {
            Definition.Resolver = fieldResolver ??
                throw new ArgumentNullException(nameof(fieldResolver));

            return ConfigureNodeField();
        }

        public IObjectFieldDescriptor ResolveNode<TId>(
            NodeResolverDelegate<object, TId> fieldResolver)
        {
            if (fieldResolver is null)
            {
                throw new ArgumentNullException(nameof(fieldResolver));
            }

            return ResolveNode(async ctx =>
            {
                if (ctx.LocalContextData.TryGetValue(
                    WellKnownContextData.InternalId,
                    out var o) && o is TId id)
                {
                    return await fieldResolver(ctx, id).ConfigureAwait(false);
                }

                return null;
            });
        }

        public IObjectFieldDescriptor ResolveNodeWith<TResolver>(
            Expression<Func<TResolver, object?>> method)
        {
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            MemberInfo member = method.TryExtractMember();

            if (member is MethodInfo m)
            {
                FieldResolverDelegates resolver =
                    Context.ResolverCompiler.CompileResolve(
                        m,
                        typeof(object),
                        typeof(TResolver));
                return ResolveNode(resolver.Resolver!);
            }

            throw new ArgumentException(
                TypeResources.NodeDescriptor_MustBeMethod,
                nameof(member));
        }

        public IObjectFieldDescriptor ResolveNodeWith(MethodInfo method)
        {
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            FieldResolverDelegates resolver =
                Context.ResolverCompiler.CompileResolve(
                    method,
                    typeof(object),
                    method.DeclaringType ?? typeof(object));

            return ResolveNode(resolver.Resolver!);
        }

        protected static class MiddlewareHelper
        {
            private static FieldMiddleware? _middleware;

            private static FieldMiddleware Middleware
            {
                get => _middleware ??= FieldClassMiddlewareFactory.Create<IdMiddleware>();
            }

            public static IObjectFieldDescriptor TryAdd(IObjectFieldDescriptor descriptor)
            {
                descriptor.Extend().OnBeforeCreate(d =>
                {
                    if (!d.MiddlewareComponents.Contains(Middleware))
                    {
                        d.MiddlewareComponents.Add(Middleware);
                    }
                });

                return descriptor;
            }
        }
    }
}
