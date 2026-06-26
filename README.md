# Fakebook Search Infrastructure: Database Schema & Architecture

This repository contains the core database schema and architecture of the Fakebook Search Engine. The design is inspired by Facebook's search infrastructure and focuses on fast, scalable, and privacy-aware searching.

---

## 1. Four Core Pillars of the Search System

Our search engine is built around four fundamental components inspired by Facebook's architecture.

### 1. Social Graph

Unlike traditional search engines that index web pages, Fakebook searches through a **social graph**.

The social graph consists of:

- **Nodes** (Users, Posts, Groups)
- **Edges** (Friendships, Likes, Comments, Follows, etc.)

This relationship-based structure allows the search engine to understand how different objects are connected.

---

### 2. Unicorn-style Inverted Index

Searching millions of records directly from the database would be too slow.

To solve this problem, the system uses an **Inverted Index**, similar to Facebook's Unicorn search engine.

Instead of scanning every object, the engine maps each keyword (token) to the objects containing it.

Example:

```
"football"
        ↓
Post #15
Post #42
Group #8
User #103
```

This allows search results to be retrieved in milliseconds.

---

### 3. Real-Time Index Updates

New content should become searchable almost immediately.

Whenever a user creates or updates a post, the indexing service automatically updates the search index.

This workflow is inspired by Facebook's **Wormhole** architecture, which continuously synchronizes database changes with the search index.

---

### 4. AI Search & Privacy Control

The search engine combines intelligent searching with strict access control.

#### Semantic Search

Instead of matching only exact keywords, AI models can understand similar meanings.

Example:

```
Italian coffee
        ↓
Cappuccino
Espresso
Latte
```

#### Access Control (ACL)

Every search result is filtered by privacy settings.

For example:

- Public → Everyone can see it.
- Friends → Only friends can access it.
- Private → Only the owner can view it.

This ensures users never receive results they do not have permission to access.

---

# Database Schema

The search database consists of three main tables.

## 1. Objects

Stores every searchable entity in the system.

```sql
objects
```

Main fields:

- `id` – Unique object ID
- `type` – USER, POST, GROUP
- `sort_key` – Ranking score
- `owner_id` – Object owner
- `privacy_level` – Access permission
- `created_at` – Creation timestamp

This table stores metadata used for ranking and permission filtering.

---

## 2. Tokens

Stores every unique searchable keyword.

```sql
tokens
```

Example:

| ID | Token |
|----|--------|
| 1 | football |
| 2 | coffee |
| 3 | fakebook |

Each keyword appears only once.

---

## 3. Token_Object (Inverted Index)

Connects keywords with searchable objects.

```sql
token_object
```

Example:

| Token | Object |
|--------|---------|
| football | Post #15 |
| football | Group #8 |
| coffee | Post #42 |

Instead of searching every post, the system directly looks up the related objects through this table.

---

# Search Workflow

When a user performs a search, the system follows these steps:

### Step 1. Tokenize the Query

Example:

```
"Fakebook Search"
```

↓

```
fakebook
search
```

---

### Step 2. Find Token IDs

Search each keyword inside the **Tokens** table.

Example:

```
fakebook → Token ID = 8
search → Token ID = 25
```

---

### Step 3. Retrieve Matching Objects

Use the **Token_Object** table to obtain all related object IDs.

---

### Step 4. Apply Security Filters

Join with the **Objects** table and remove objects that the current user cannot access.

Examples:

- Private posts
- Friends-only content
- Blocked users

---

### Step 5. Rank Results

Sort the remaining objects using:

- Popularity (`sort_key`)
- Relevance
- Creation time (optional)

---

### Step 6. Return Final Results

Retrieve the complete data from the main application database (Users, Posts, Groups) and return the final search results to the user.

---

# Key Features

- Fast keyword search using an Inverted Index
- Scalable architecture for millions of objects
- Real-time indexing
- Privacy-aware search
- AI-powered semantic search
- Flexible support for multiple object types
- Simple and extensible database design

---

# Future Improvements

Possible enhancements include:

- Full-text ranking (TF-IDF / BM25)
- Vector search using embeddings
- Personalized search ranking
- Typo correction
- Search suggestions (Autocomplete)
- Trending search keywords
- Distributed indexing and sharding
- Elasticsearch integration
