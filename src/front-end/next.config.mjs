/** @type {import('next').NextConfig} */
const nextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://127.0.0.1:5264/api/:path*",
      },
    ];
  },
  allowedDevOrigins: ["*.ngrok-free.dev"],
};

export default nextConfig;
