CREATE TABLE users (
                       id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                       email VARCHAR(255) UNIQUE NOT NULL,
                       password_hash VARCHAR(255) NOT NULL,
                       is_admin BOOLEAN NOT NULL DEFAULT false,
                       created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);
CREATE TABLE refresh_tokens (
                                  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                                  user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                                  token VARCHAR(255) UNIQUE NOT NULL,
                                  expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
                                  created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                                  revoked_at TIMESTAMP WITH TIME ZONE NULL
  );