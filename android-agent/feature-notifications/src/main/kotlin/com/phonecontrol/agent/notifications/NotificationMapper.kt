package com.phonecontrol.agent.notifications

data class NotificationEvent(
    val key: String,
    val packageName: String,
    val postedAt: Long
)

class NotificationMapper {
    fun map(key: String, packageName: String, postedAt: Long): NotificationEvent {
        require(key.isNotBlank())
        require(packageName.isNotBlank())
        return NotificationEvent(key, packageName, postedAt)
    }
}
