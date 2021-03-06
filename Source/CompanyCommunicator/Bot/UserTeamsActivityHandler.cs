// <copyright file="UserTeamsActivityHandler.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// </copyright>

namespace Microsoft.Teams.Apps.CompanyCommunicator.Bot
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Teams;
    using Microsoft.Bot.Schema;
    using Microsoft.Bot.Schema.Teams;
    using Microsoft.Teams.Apps.CompanyCommunicator.Common.Repositories.ReactionData;
    using Microsoft.Teams.Apps.CompanyCommunicator.Repositories.Extensions;

    /// <summary>
    /// Company Communicator User Bot.
    /// Captures user data, team data.
    /// </summary>
    public class UserTeamsActivityHandler : TeamsActivityHandler
    {
        private static readonly string TeamRenamedEventType = "teamRenamed";
        private readonly IReactionDataRepository reactionDataRepository;

        private readonly TeamsDataCapture teamsDataCapture;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserTeamsActivityHandler"/> class.
        /// </summary>
        /// <param name="teamsDataCapture">Teams data capture service.</param>
        public UserTeamsActivityHandler(TeamsDataCapture teamsDataCapture)
        {
            this.teamsDataCapture = teamsDataCapture ?? throw new ArgumentNullException(nameof(teamsDataCapture));
            this.reactionDataRepository = reactionDataRepository ?? throw new ArgumentNullException(nameof(reactionDataRepository));
        }
        /// <inheritdoc/>
        protected override async Task OnMessageReactionActivityAsync(ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ReactionsAdded != null)
            {
                await this.OnReactionsAddedAsync(turnContext.Activity.ReactionsAdded, turnContext, cancellationToken);
            }

            if (turnContext.Activity.ReactionsRemoved != null)
            {
                await this.OnReactionsRemovedAsync(turnContext.Activity.ReactionsAdded, turnContext, cancellationToken);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnReactionsAddedAsync(IList<MessageReaction> messageReactions, ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var reaction in messageReactions)
            {
                // The ReplyToId property of the inbound MessageReaction Activity will correspond to a Message Activity which
                // had previously been sent from this bot.
                var activity = turnContext.Activity;
                if (activity == null)
                {
                    // If we had sent the message from the error handler we wouldn't have recorded the Activity Id and so we
                    // shouldn't expect to see it in the log.
                    throw new NotImplementedException();
                }

                await this.reactionDataRepository.SaveReactionDataAsync(reaction.Type, activity);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnReactionsRemovedAsync(IList<MessageReaction> messageReactions, ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var reaction in messageReactions)
            {
                // The ReplyToId property of the inbound MessageReaction Activity will correspond to a Message Activity which
                // was previously sent from this bot.
                var activity = turnContext.Activity;
                if (activity == null)
                {
                    // If we had sent the message from the error handler we wouldn't have recorded the Activity Id and so we
                    // shouldn't expect to see it in the log.
                    throw new NotImplementedException();
                }

                await this.reactionDataRepository.RemoveReactionDataAsync(reaction.Type, activity);
            }
        }

        /// <summary>
        /// Invoked when a conversation update activity is received from the channel.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task OnConversationUpdateActivityAsync(
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            // base.OnConversationUpdateActivityAsync is useful when it comes to responding to users being added to or removed from the conversation.
            // For example, a bot could respond to a user being added by greeting the user.
            // By default, base.OnConversationUpdateActivityAsync will call <see cref="OnMembersAddedAsync(IList{ChannelAccount}, ITurnContext{IConversationUpdateActivity}, CancellationToken)"/>
            // if any users have been added or <see cref="OnMembersRemovedAsync(IList{ChannelAccount}, ITurnContext{IConversationUpdateActivity}, CancellationToken)"/>
            // if any users have been removed. base.OnConversationUpdateActivityAsync checks the member ID so that it only responds to updates regarding members other than the bot itself.
            await base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);

            var activity = turnContext.Activity;

            var isTeamRenamed = this.IsTeamInformationUpdated(activity);
            if (isTeamRenamed)
            {
                await this.teamsDataCapture.OnTeamInformationUpdatedAsync(activity);
            }

            if (activity.MembersAdded != null)
            {
                await this.teamsDataCapture.OnBotAddedAsync(turnContext, activity, cancellationToken);
            }

            if (activity.MembersRemoved != null)
            {
                await this.teamsDataCapture.OnBotRemovedAsync(activity);
            }
        }

        private bool IsTeamInformationUpdated(IConversationUpdateActivity activity)
        {
            if (activity == null)
            {
                return false;
            }

            var channelData = activity.GetChannelData<TeamsChannelData>();
            if (channelData == null)
            {
                return false;
            }

            return UserTeamsActivityHandler.TeamRenamedEventType.Equals(channelData.EventType, StringComparison.OrdinalIgnoreCase);
        }
    }
}