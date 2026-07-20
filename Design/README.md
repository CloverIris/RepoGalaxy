# RepoGalaxy product design

## Core promise

Help a developer continually discover worthwhile GitHub projects and turn their interest into a readable feed, an organized library, and timely updates.

## Information architecture

The shell contains Discover, Subscriptions, Library, Notifications, My Repositories, Local Repositories, and Settings.

Discover has three sources: For you, Subscriptions, and Trending. Each feed item records its source, discovery time, recommendation reason, score, read state, and dismissed state. Selecting an item exposes repository context and its recommendation explanation.

Subscriptions are explicit topic, language, and keyword rules. The library data model supports collection names, tags, and notes. Notifications surface high-match discoveries and a saved repository's new stable release while the desktop process is alive.

## Visual direction

Use Avalonia's built-in Fluent controls, system light/dark mode, compact repository lists, and a focused detail pane. The retired visual language is not part of the product.

## Data and lifecycle

The v2 SQLite database is a new store. No old bookmarks, history, preferences, or local repository data is migrated. Sync is session-bound: closing the application disposes the timer and ends all sync and notification activity.
