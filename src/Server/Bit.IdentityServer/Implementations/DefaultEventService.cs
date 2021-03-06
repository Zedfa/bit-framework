﻿using Bit.Core.Contracts;
using IdentityServer3.Core.Events;
using IdentityServer3.Core.Services;
using System.Threading.Tasks;

namespace Bit.IdentityServer.Implementations
{
    public class DefaultEventService : IEventService
    {
        private readonly IDependencyManager _dependencyManager;

#if DEBUG
        protected DefaultEventService()
        {
        }
#endif

        public DefaultEventService(IDependencyManager dependencyManager)
        {
            _dependencyManager = dependencyManager;
        }

        public virtual async Task RaiseAsync<T>(Event<T> evt)
        {
            if (evt.EventType == EventTypes.Error || evt.EventType == EventTypes.Failure)
            {
                using (Core.Contracts.IDependencyResolver resolver = _dependencyManager.CreateChildDependencyResolver())
                {
                    ILogger logger = resolver.Resolve<ILogger>();

                    logger.AddLogData(nameof(EventContext.ActivityId), evt.Context.ActivityId);
                    logger.AddLogData(nameof(EventContext.RemoteIpAddress), evt.Context.RemoteIpAddress);
                    logger.AddLogData(nameof(EventContext.SubjectId), evt.Context.SubjectId);
                    logger.AddLogData(nameof(Event<object>.Category), evt.Category);
                    logger.AddLogData("IdentityServerEventId", evt.Id);
                    logger.AddLogData(nameof(Event<object>.Name), evt.Name);

                    await logger.LogFatalAsync(evt.Message).ConfigureAwait(false);
                }
            }
        }
    }
}
