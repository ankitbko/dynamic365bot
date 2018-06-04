namespace Microsoft.Dynamics.Dynamics365Bot
{
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Respond with a help message when begining interaction with the Bot.
    /// </summary>
    public static class Response
    {
        /// <summary>
        /// Return a help message to the user
        /// With a list of operations that the Bot can perform.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Activity</returns>
        public static Activity HelpMessage(Activity message)
        {
            message.Text =
                @"I can search and display cases, opportunities or views.  
                - **show me case having id CAS-1234** where *CAS-1234* is case number  
                - **what are opportunities from Wallmart** where *Wallmart* is account name  
                - **get all active cases**  
                - **what is status of Wallmart deal** where *Wallmart* is account name";
            return message;
        }
    }
}