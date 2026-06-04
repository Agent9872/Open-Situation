-- ============================================================
-- OPEN SITUATION — MASTER SUPABASE SCHEMA
-- Save this file to your GitHub repo as:
-- /supabase/migrations/20240604_master_schema.sql
-- ============================================================

-- =====================
-- POSTS
-- =====================
create table if not exists public."Posts" (
  "Id"                      serial primary key,
  "AuthorPhone"             text not null,
  "Content"                 text default '',
  "Category"                text default '',
  "Visibility"              text default 'Everyone',
  "ImagePathsJson"          text default '[]',
  "Mood"                    text default '',
  "StatusImagePath"         text default '',
  "AuthorLookingFor"        text default '',
  "AuthorProfileImagePath"  text default '',
  "AuthorDisplayName"       text default '',
  "IsCurrentUserPost"       boolean default false,
  "HiddenByJson"            text default '[]',
  "LoveCount"               int default 0,
  "LovedByJson"             text default '[]',
  "SparkCount"              int default 0,
  "SparkedByJson"           text default '[]',
  "IsSavedByCurrentUser"    boolean default false,
  "IsAuthorVerified"        boolean default false,
  "CreatedAt"               timestamptz default now()
);

-- =====================
-- USER PHOTOS
-- =====================
create table if not exists public."UserPhotos" (
  "Id"          serial primary key,
  "UserId"      int not null,
  "ImagePath"   text default '',
  "Caption"     text default '',
  "Order"       int default 0,
  "IsPrimary"   boolean default false,
  "Category"    text default 'Profile',
  "UploadedAt"  timestamptz default now()
);

-- =====================
-- USER PROMPTS
-- =====================
create table if not exists public."UserPrompts" (
  "Id"        serial primary key,
  "UserId"    int not null,
  "Question"  text default '',
  "Answer"    text default '',
  "Order"     int default 0,
  "CreatedAt" timestamptz default now()
);

-- =====================
-- DATE IDEAS
-- =====================
create table if not exists public."DateIdeas" (
  "Id"          serial primary key,
  "UserId"      int not null,
  "Title"       text default '',
  "Description" text default '',
  "Location"    text default '',
  "Latitude"    float default 0,
  "Longitude"   float default 0,
  "Category"    text default '',
  "IsPublic"    boolean default false,
  "Likes"       int default 0,
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- USER EVENTS
-- =====================
create table if not exists public."UserEvents" (
  "Id"            serial primary key,
  "UserId"        int not null,
  "EventName"     text default '',
  "Description"   text default '',
  "Location"      text default '',
  "Latitude"      float default 0,
  "Longitude"     float default 0,
  "EventDate"     timestamptz,
  "Category"      text default '',
  "MaxAttendees"  int default 0,
  "IsPublic"      boolean default false,
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- EVENT ATTENDANCE
-- =====================
create table if not exists public."EventAttendance" (
  "Id"        serial primary key,
  "EventId"   int not null,
  "UserId"    int not null,
  "JoinedAt"  timestamptz default now()
);

-- =====================
-- USER BLOCKS
-- =====================
create table if not exists public."UserBlocks" (
  "Id"            serial primary key,
  "BlockerPhone"  text not null,
  "BlockedPhone"  text not null,
  "BlockedAt"     timestamptz default now()
);

-- =====================
-- BLOCKED USERS
-- =====================
create table if not exists public."BlockedUsers" (
  "Id"            serial primary key,
  "BlockerPhone"  text not null,
  "BlockedPhone"  text not null,
  "BlockedAt"     timestamptz default now()
);

-- =====================
-- SEEN POSTS
-- =====================
create table if not exists public."SeenPosts" (
  "Id"        serial primary key,
  "UserPhone" text not null,
  "PostId"    int not null,
  "SeenAt"    timestamptz default now()
);

-- =====================
-- EMERGENCY CONTACTS
-- =====================
create table if not exists public."EmergencyContacts" (
  "Id"            serial primary key,
  "UserPhone"     text not null,
  "Name"          text default '',
  "Phone"         text default '',
  "Relationship"  text default '',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- LIVE SESSIONS
-- =====================
create table if not exists public."LiveSessions" (
  "Id"        serial primary key,
  "HostPhone" text not null,
  "Title"     text default '',
  "IsActive"  boolean default true,
  "StartedAt" timestamptz default now(),
  "EndedAt"   timestamptz
);

-- =====================
-- SPARK RATE LIMITS
-- =====================
create table if not exists public."SparkRateLimits" (
  "Id"          serial primary key,
  "UserPhone"   text not null,
  "PostId"      int not null,
  "SparkCount"  int default 0,
  "LastSparkAt" timestamptz default now()
);

-- =====================
-- SPARK TRANSACTIONS
-- =====================
create table if not exists public."SparkTransactions" (
  "Id"            serial primary key,
  "SenderPhone"   text not null,
  "PostId"        int not null,
  "Amount"        int default 1,
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- COIN TRANSACTIONS
-- =====================
create table if not exists public."CoinTransactions" (
  "Id"            serial primary key,
  "UserPhone"     text not null,
  "Amount"        int default 0,
  "Type"          text default '',
  "Description"   text default '',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- COMMENTS
-- =====================
create table if not exists public."Comments" (
  "Id"          serial primary key,
  "PostId"      int not null,
  "AuthorPhone" text not null,
  "Content"     text default '',
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- CONVERSATIONS
-- =====================
create table if not exists public."Conversations" (
  "Id"                  serial primary key,
  "ParticipantOnePhone" text not null,
  "ParticipantTwoPhone" text not null,
  "LastMessage"         text default '',
  "LastMessageAt"       timestamptz default now(),
  "CreatedAt"           timestamptz default now()
);

-- =====================
-- CHAT MESSAGES
-- =====================
create table if not exists public."ChatMessages" (
  "Id"              serial primary key,
  "ConversationId"  int not null,
  "SenderPhone"     text not null,
  "Content"         text default '',
  "MediaPath"       text default '',
  "MediaType"       text default '',
  "MessageType"     text default 'text',
  "IsRead"          boolean default false,
  "SentAt"          timestamptz default now()
);

-- =====================
-- MESSAGE REQUESTS
-- =====================
create table if not exists public."MessageRequests" (
  "Id"            serial primary key,
  "SenderPhone"   text not null,
  "ReceiverPhone" text not null,
  "Status"        text default 'pending',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- GROUPS
-- =====================
create table if not exists public."Groups" (
  "Id"            serial primary key,
  "GroupId"       text unique not null,
  "Name"          text default '',
  "Description"   text default '',
  "AvatarPath"    text default '',
  "CreatorPhone"  text not null,
  "IsPublic"      boolean default true,
  "MemberCount"   int default 0,
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- GROUP MEMBERS
-- =====================
create table if not exists public."GroupMembers" (
  "Id"        serial primary key,
  "GroupId"   text not null,
  "UserPhone" text not null,
  "Role"      text default 'member',
  "JoinedAt"  timestamptz default now()
);

-- =====================
-- GROUP MESSAGES
-- =====================
create table if not exists public."GroupMessages" (
  "Id"          serial primary key,
  "GroupId"     text not null,
  "SenderPhone" text not null,
  "Content"     text default '',
  "MediaPath"   text default '',
  "MediaType"   text default '',
  "MessageType" text default 'text',
  "IsRead"      boolean default false,
  "SentAt"      timestamptz default now()
);

-- =====================
-- GROUP INVITES
-- =====================
create table if not exists public."GroupInvites" (
  "Id"            serial primary key,
  "GroupId"       text not null,
  "InviterPhone"  text not null,
  "InviteePhone"  text not null,
  "Status"        text default 'pending',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- GROUP JOIN REQUESTS
-- =====================
create table if not exists public."GroupJoinRequests" (
  "Id"              serial primary key,
  "GroupId"         text not null,
  "RequesterPhone"  text not null,
  "Status"          text default 'pending',
  "CreatedAt"       timestamptz default now()
);

-- =====================
-- GROUP EVENTS
-- =====================
create table if not exists public."GroupEvents" (
  "Id"          serial primary key,
  "GroupId"     text not null,
  "Title"       text default '',
  "Description" text default '',
  "EventDate"   timestamptz,
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- GROUP PINNED MESSAGES
-- =====================
create table if not exists public."GroupPinnedMessages" (
  "Id"        serial primary key,
  "GroupId"   text not null,
  "MessageId" int not null,
  "PinnedAt"  timestamptz default now()
);

-- =====================
-- FOLLOWS
-- =====================
create table if not exists public."Follows" (
  "Id"            serial primary key,
  "FollowerPhone" text not null,
  "FollowedPhone" text not null,
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- REPORTS
-- =====================
create table if not exists public."Reports" (
  "Id"            serial primary key,
  "ReporterPhone" text not null,
  "TargetPhone"   text default '',
  "PostId"        int default 0,
  "Reason"        text default '',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- TAGS
-- =====================
create table if not exists public."Tags" (
  "Id"        serial primary key,
  "Name"      text default '',
  "CreatedAt" timestamptz default now()
);

-- =====================
-- PROJECTS
-- =====================
create table if not exists public."Projects" (
  "Id"          serial primary key,
  "UserPhone"   text not null,
  "Title"       text default '',
  "Description" text default '',
  "IsPublic"    boolean default true,
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- PROJECTS TAGS
-- =====================
create table if not exists public."ProjectsTags" (
  "Id"        serial primary key,
  "ProjectId" int not null,
  "TagId"     int not null,
  "CreatedAt" timestamptz default now()
);

-- =====================
-- PROJECT TASKS
-- =====================
create table if not exists public."ProjectTasks" (
  "Id"          serial primary key,
  "ProjectId"   int not null,
  "Title"       text default '',
  "IsCompleted" boolean default false,
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- GIFT DEFINITIONS
-- =====================
create table if not exists public."GiftDefinitions" (
  "Id"          serial primary key,
  "Name"        text default '',
  "Description" text default '',
  "CoinCost"    int default 0,
  "ImagePath"   text default '',
  "CreatedAt"   timestamptz default now()
);

-- =====================
-- ENDORSEMENT REQUESTS
-- =====================
create table if not exists public."EndorsementRequests" (
  "Id"              serial primary key,
  "RequesterPhone"  text not null,
  "TargetPhone"     text not null,
  "Skill"           text default '',
  "Status"          text default 'pending',
  "CreatedAt"       timestamptz default now()
);

-- =====================
-- PENDING ENDORSEMENTS
-- =====================
create table if not exists public."PendingEndorsements" (
  "Id"            serial primary key,
  "UserPhone"     text not null,
  "EndorserPhone" text not null,
  "Skill"         text default '',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- USER ENDORSEMENTS
-- =====================
create table if not exists public."UserEndorsements" (
  "Id"            serial primary key,
  "UserPhone"     text not null,
  "EndorserPhone" text not null,
  "Skill"         text default '',
  "CreatedAt"     timestamptz default now()
);

-- =====================
-- PROFILE VIEWS
-- =====================
create table if not exists public."ProfileViews" (
  "Id"          serial primary key,
  "ViewerPhone" text not null,
  "ViewedPhone" text not null,
  "ViewedAt"    timestamptz default now()
);

-- =====================
-- SEARCH HISTORY
-- =====================
create table if not exists public."SearchHistory" (
  "Id"         serial primary key,
  "UserPhone"  text not null,
  "Query"      text default '',
  "SearchedAt" timestamptz default now()
);

-- =====================
-- NOTIFICATIONS
-- =====================
create table if not exists public."Notifications" (
  "Id"        serial primary key,
  "UserPhone" text not null,
  "Title"     text default '',
  "Body"      text default '',
  "Type"      text default '',
  "IsRead"    boolean default false,
  "CreatedAt" timestamptz default now()
);

-- =====================
-- ADMIN TRACKING
-- =====================
create table if not exists public."UserMoodTracking" (
  "Id"        serial primary key,
  "UserPhone" text not null,
  "OldMood"   text default '',
  "NewMood"   text default '',
  "Timestamp" timestamptz default now()
);

create table if not exists public."UserProfileTracking" (
  "Id"            serial primary key,
  "UserPhone"     text not null,
  "FieldChanged"  text default '',
  "OldValue"      text default '',
  "NewValue"      text default '',
  "Timestamp"     timestamptz default now()
);

create table if not exists public."UserLoginTracking" (
  "Id"          serial primary key,
  "UserPhone"   text not null,
  "LoginTime"   timestamptz default now(),
  "IpAddress"   text default '',
  "DeviceInfo"  text default ''
);

create table if not exists public."PostTracking" (
  "Id"          serial primary key,
  "AuthorPhone" text not null,
  "PostId"      int not null,
  "Action"      text default '',
  "Timestamp"   timestamptz default now()
);

create table if not exists public."GroupTracking" (
  "Id"          serial primary key,
  "GroupId"     text not null,
  "ActorPhone"  text not null,
  "Action"      text default '',
  "Timestamp"   timestamptz default now()
);

-- ============================================================
-- ROW LEVEL SECURITY — ENABLE ON ALL TABLES
-- ============================================================
alter table public."Posts" enable row level security;
alter table public."UserPhotos" enable row level security;
alter table public."UserPrompts" enable row level security;
alter table public."DateIdeas" enable row level security;
alter table public."UserEvents" enable row level security;
alter table public."EventAttendance" enable row level security;
alter table public."UserBlocks" enable row level security;
alter table public."BlockedUsers" enable row level security;
alter table public."SeenPosts" enable row level security;
alter table public."EmergencyContacts" enable row level security;
alter table public."LiveSessions" enable row level security;
alter table public."SparkRateLimits" enable row level security;
alter table public."SparkTransactions" enable row level security;
alter table public."CoinTransactions" enable row level security;
alter table public."Comments" enable row level security;
alter table public."Conversations" enable row level security;
alter table public."ChatMessages" enable row level security;
alter table public."MessageRequests" enable row level security;
alter table public."Groups" enable row level security;
alter table public."GroupMembers" enable row level security;
alter table public."GroupMessages" enable row level security;
alter table public."GroupInvites" enable row level security;
alter table public."GroupJoinRequests" enable row level security;
alter table public."GroupEvents" enable row level security;
alter table public."GroupPinnedMessages" enable row level security;
alter table public."Follows" enable row level security;
alter table public."Reports" enable row level security;
alter table public."Tags" enable row level security;
alter table public."Projects" enable row level security;
alter table public."ProjectsTags" enable row level security;
alter table public."ProjectTasks" enable row level security;
alter table public."GiftDefinitions" enable row level security;
alter table public."EndorsementRequests" enable row level security;
alter table public."PendingEndorsements" enable row level security;
alter table public."UserEndorsements" enable row level security;
alter table public."ProfileViews" enable row level security;
alter table public."SearchHistory" enable row level security;
alter table public."Notifications" enable row level security;
alter table public."UserMoodTracking" enable row level security;
alter table public."UserProfileTracking" enable row level security;
alter table public."UserLoginTracking" enable row level security;
alter table public."PostTracking" enable row level security;
alter table public."GroupTracking" enable row level security;

-- ============================================================
-- POLICIES — SAFE CREATE (checks before creating)
-- ============================================================
do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Posts' and policyname = 'Allow all') then
    create policy "Allow all" on public."Posts" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserPhotos' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserPhotos" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserPrompts' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserPrompts" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'DateIdeas' and policyname = 'Allow all') then
    create policy "Allow all" on public."DateIdeas" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserEvents' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserEvents" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'EventAttendance' and policyname = 'Allow all') then
    create policy "Allow all" on public."EventAttendance" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserBlocks' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserBlocks" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'BlockedUsers' and policyname = 'Allow all') then
    create policy "Allow all" on public."BlockedUsers" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'SeenPosts' and policyname = 'Allow all') then
    create policy "Allow all" on public."SeenPosts" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'EmergencyContacts' and policyname = 'Allow all') then
    create policy "Allow all" on public."EmergencyContacts" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'LiveSessions' and policyname = 'Allow all') then
    create policy "Allow all" on public."LiveSessions" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'SparkRateLimits' and policyname = 'Allow all') then
    create policy "Allow all" on public."SparkRateLimits" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'SparkTransactions' and policyname = 'Allow all') then
    create policy "Allow all" on public."SparkTransactions" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'CoinTransactions' and policyname = 'Allow all') then
    create policy "Allow all" on public."CoinTransactions" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Comments' and policyname = 'Allow all') then
    create policy "Allow all" on public."Comments" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Conversations' and policyname = 'Allow all') then
    create policy "Allow all" on public."Conversations" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'ChatMessages' and policyname = 'Allow all') then
    create policy "Allow all" on public."ChatMessages" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'MessageRequests' and policyname = 'Allow all') then
    create policy "Allow all" on public."MessageRequests" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Groups' and policyname = 'Allow all') then
    create policy "Allow all" on public."Groups" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupMembers' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupMembers" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupMessages' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupMessages" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupInvites' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupInvites" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupJoinRequests' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupJoinRequests" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupEvents' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupEvents" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupPinnedMessages' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupPinnedMessages" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Follows' and policyname = 'Allow all') then
    create policy "Allow all" on public."Follows" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Reports' and policyname = 'Allow all') then
    create policy "Allow all" on public."Reports" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Tags' and policyname = 'Allow all') then
    create policy "Allow all" on public."Tags" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Projects' and policyname = 'Allow all') then
    create policy "Allow all" on public."Projects" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'ProjectsTags' and policyname = 'Allow all') then
    create policy "Allow all" on public."ProjectsTags" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'ProjectTasks' and policyname = 'Allow all') then
    create policy "Allow all" on public."ProjectTasks" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GiftDefinitions' and policyname = 'Allow all') then
    create policy "Allow all" on public."GiftDefinitions" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'EndorsementRequests' and policyname = 'Allow all') then
    create policy "Allow all" on public."EndorsementRequests" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'PendingEndorsements' and policyname = 'Allow all') then
    create policy "Allow all" on public."PendingEndorsements" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserEndorsements' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserEndorsements" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'ProfileViews' and policyname = 'Allow all') then
    create policy "Allow all" on public."ProfileViews" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'SearchHistory' and policyname = 'Allow all') then
    create policy "Allow all" on public."SearchHistory" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'Notifications' and policyname = 'Allow all') then
    create policy "Allow all" on public."Notifications" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserMoodTracking' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserMoodTracking" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserProfileTracking' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserProfileTracking" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'UserLoginTracking' and policyname = 'Allow all') then
    create policy "Allow all" on public."UserLoginTracking" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'PostTracking' and policyname = 'Allow all') then
    create policy "Allow all" on public."PostTracking" for all using (true); end if;
end $$;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'GroupTracking' and policyname = 'Allow all') then
    create policy "Allow all" on public."GroupTracking" for all using (true); end if;
end $$;
