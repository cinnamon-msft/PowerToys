#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has DoNotDisturb enabled or disabled
    static void EnableDoNotDisturb(const bool enabled) noexcept;
};
