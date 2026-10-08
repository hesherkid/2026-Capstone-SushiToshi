import { Alert, Box, Paper, Typography } from "@mui/material";

const AuthCard = ({
    title,
    subtitle,
    error,
    success,
    maxWidth = 500,
    gap = { xs: 2.5, sm: 3 },
    children,
    footer,
}) => {
    return (
        <Box
            sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                padding: { xs: 2, sm: 4, md: 6 },
            }}
        >
            <Paper
                sx={{
                    padding: { xs: 3, sm: 5, md: 6 },
                    width: "100%",
                    maxWidth,
                    backgroundColor: "white",
                    borderRadius: 2,
                }}
            >
                <Box
                    sx={{
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                        gap,
                    }}
                >
                    {(title || subtitle) && (
                        <Box sx={{ textAlign: "center" }}>
                            {title && (
                                <Typography
                                    component="h1"
                                    sx={{
                                        fontWeight: "bold",
                                        color: "#111827",
                                        fontSize: { xs: "1.5rem", sm: "2rem" },
                                    }}
                                >
                                    {title}
                                </Typography>
                            )}

                            {subtitle && (
                                <Typography
                                    variant="body1"
                                    sx={{
                                        color: "#4b5563",
                                        mt: title ? 1 : 0,
                                    }}
                                >
                                    {subtitle}
                                </Typography>
                            )}
                        </Box>
                    )}

                    {error && (
                        <Alert severity="error" sx={{ width: "100%", borderRadius: 2 }}>
                            {error}
                        </Alert>
                    )}

                    {success && (
                        <Alert severity="success" sx={{ width: "100%", borderRadius: 2 }}>
                            {success}
                        </Alert>
                    )}

                    {children}

                    {footer && <Box>{footer}</Box>}
                </Box>
            </Paper>
        </Box>
    );
};

export default AuthCard;
