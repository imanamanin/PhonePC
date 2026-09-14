package com.phonecontrol.agent.notifications

import android.service.notification.NotificationListenerService
import android.service.notification.StatusBarNotification

class AgentNotificationListener : NotificationListenerService() {
    override fun onNotificationPosted(sbn: StatusBarNotification?) {
        // Mapping happens in Phase 5. Do not log extras/text.
    }

    override fun onNotificationRemoved(sbn: StatusBarNotification?) = Unit
}
