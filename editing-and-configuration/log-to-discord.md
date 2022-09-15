# Log to Discord

In BoRAT, we log to Discord using RequestBin which is now known as Pipedream.

### Creating a Pipedream Account

1. Go to [https://pipedream.com/auth/signup](https://pipedream.com/auth/signup).
2. Create an account using the form or with Google or GitHub.
3. After a successful login, select `HTTP / Webhook` as the trigger.
4. Select `HTTP Requests (Most Popular)`.
5. `Click Save and continue` and `Generate Test Event`.
6. Click Continue and search for `Discord Webhook`.
7. Select the normal `Send Message` action.
8. For the `Message` content use the [`Pipedream Configuration`](log-to-discord.md#pipedream-configuration) below.

### Pipedream Configuration

```javascript
** **
**!!! NEW USER HAS BEEN PWNED !!!**
Public IP: **{{steps.trigger.event.body.pubip}}**
Username: **{{steps.trigger.event.body.username}}**
OS: **{{steps.trigger.event.body.os}}**
Security: **{{steps.trigger.event.body.security}}**
Date/Time: **{{steps.trigger.event.body.datetime}}**
** **
```

### Adding the Endpoint

1. Edit the frmMain.cs file inside of the Server.
2. Enable Pipedream by changing `false` to `true`.
3. Change the Pipedream URL to your own one.
